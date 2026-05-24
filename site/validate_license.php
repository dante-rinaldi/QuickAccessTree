<?php
/**
 * Sidebar Buddy — License Validation Endpoint
 *
 * Called by the desktop app to activate a license.
 * Verifies email + key, enforces the 2-machine limit, records the device.
 *
 * POST body (JSON): {
 *   "email":       "...",
 *   "key":         "SB-XXXX-XXXX-XXXX-XXXX",
 *   "device_id":   "<40-char hex>",
 *   "mac_address": "aa:bb:cc:dd:ee:ff",
 *   "hostname":    "DESKTOP-XXX"
 * }
 * Response: { "valid": true }
 *       or  { "valid": false, "error": "...", "error_code": "..." }
 */

require_once __DIR__ . '/private/secrets.php';
require_once __DIR__ . '/private/resend_mailer.php';

header('Content-Type: application/json');

define('ACTIVATION_LIMIT', 2);

if ($_SERVER['REQUEST_METHOD'] !== 'POST') {
    http_response_code(405);
    echo json_encode(['valid' => false, 'error' => 'Method not allowed']);
    exit;
}

$input     = json_decode(file_get_contents('php://input'), true);
$email     = filter_var($input['email'] ?? '', FILTER_SANITIZE_EMAIL);
$key       = trim($input['key'] ?? '');
$device_id = trim($input['device_id'] ?? '');
$mac_raw   = trim($input['mac_address'] ?? '');
$hostname  = substr(trim($input['hostname'] ?? ''), 0, 255);

$mac = preg_match('/^([0-9a-f]{2}:){5}[0-9a-f]{2}$/i', $mac_raw) ? strtolower($mac_raw) : null;

if (!filter_var($email, FILTER_VALIDATE_EMAIL) || strlen($key) < 8) {
    http_response_code(400);
    echo json_encode(['valid' => false, 'error' => 'Invalid input']);
    exit;
}

if (!preg_match('/^SB-[A-Z2-9]{4}-[A-Z2-9]{4}-[A-Z2-9]{4}-[A-Z2-9]{4}$/', $key)) {
    echo json_encode(['valid' => false, 'error' => 'Invalid key format. Keys look like: SB-XXXX-XXXX-XXXX-XXXX']);
    exit;
}

// Rate limit: 10 attempts per IP per 10 minutes
$rawIp = $_SERVER['HTTP_X_FORWARDED_FOR'] ?? $_SERVER['REMOTE_ADDR'] ?? 'unknown';
$ip    = trim(explode(',', $rawIp)[0]);
$rlKey = sys_get_temp_dir() . '/sb_lic_rl_' . md5($ip) . '.json';
$now   = time();
$rl    = file_exists($rlKey) ? (json_decode(file_get_contents($rlKey), true) ?: []) : [];
$rl    = array_values(array_filter($rl, fn($t) => $now - $t < 600));
if (count($rl) >= 10) {
    http_response_code(429);
    echo json_encode(['valid' => false, 'error' => 'Too many attempts. Please try again later.']);
    exit;
}
$rl[] = $now;
file_put_contents($rlKey, json_encode($rl), LOCK_EX);

try {
    $pdo = new PDO(
        'mysql:host=' . DB_HOST . ';dbname=' . DB_NAME . ';charset=utf8mb4',
        DB_USER, DB_PASS,
        [PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION, PDO::ATTR_DEFAULT_FETCH_MODE => PDO::FETCH_ASSOC]
    );
} catch (PDOException $e) {
    http_response_code(500);
    echo json_encode(['valid' => false, 'error' => 'Server error']);
    exit;
}

$revalidate = !empty($input['revalidate']);

// Verify email + key exist and are active
$stmt = $pdo->prepare('SELECT id FROM licenses WHERE email = ? AND license_key = ? AND status = ? LIMIT 1');
$stmt->execute([$email, $key, 'active']);
if (!$stmt->fetch()) {
    // Distinguish revoked from not found
    $chk = $pdo->prepare('SELECT status FROM licenses WHERE email = ? AND license_key = ? LIMIT 1');
    $chk->execute([$email, $key]);
    $row = $chk->fetch();
    if ($row && $row['status'] === 'revoked')
        echo json_encode(['valid' => false, 'error' => 'This license has been revoked. Please contact support.']);
    else
        echo json_encode(['valid' => false, 'error' => 'License not found for this email and key.']);
    exit;
}

// Revalidation mode: lightweight check only — no device upsert
if ($revalidate) {
    echo json_encode(['valid' => true]);
    exit;
}

// Activation limit check (only when a device_id is provided)
if ($device_id && preg_match('/^[0-9a-f]{32,64}$/', $device_id)) {

    $existing = $pdo->prepare(
        'SELECT id FROM license_activations WHERE license_key = ? AND device_id = ? LIMIT 1'
    );
    $existing->execute([$key, $device_id]);
    $alreadyActivated = (bool)$existing->fetch();

    if (!$alreadyActivated) {
        $countStmt = $pdo->prepare('SELECT COUNT(*) FROM license_activations WHERE license_key = ?');
        $countStmt->execute([$key]);
        $count = (int)$countStmt->fetchColumn();

        if ($count >= ACTIVATION_LIMIT) {
            echo json_encode([
                'valid'      => false,
                'error_code' => 'activation_limit',
                'error'      => 'This license is active on ' . ACTIVATION_LIMIT . ' computers — the maximum allowed. '
                              . 'Email support@sidebarbuddy.com to free a slot.',
            ]);
            exit;
        }
    }

    // IP geolocation (best-effort)
    $country = null;
    if ($ip && $ip !== '127.0.0.1' && $ip !== '::1') {
        $geo = @file_get_contents("http://ip-api.com/json/{$ip}?fields=country");
        if ($geo) $country = json_decode($geo, true)['country'] ?? null;
    }

    // Upsert activation record
    $pdo->prepare(
        'INSERT INTO license_activations (license_key, email, device_id, mac_address, hostname, ip_address, country)
             VALUES (?, ?, ?, ?, ?, ?, ?)
         ON DUPLICATE KEY UPDATE
             last_seen   = NOW(),
             ip_address  = VALUES(ip_address),
             hostname    = COALESCE(VALUES(hostname), hostname),
             mac_address = COALESCE(mac_address, VALUES(mac_address)),
             country     = COALESCE(country, VALUES(country))'
    )->execute([$key, $email, $device_id, $mac, $hostname ?: null, $ip, $country]);

    // Notify buyer of new machine activation (not on reinstall)
    if (!$alreadyActivated) {
        $subject = 'Sidebar Buddy — New Machine Activation';
        $body    = "Your Sidebar Buddy license was activated on a new computer.\n\n"
                 . "  Computer: " . ($hostname ?: 'unknown') . "\n"
                 . "  Location: " . ($country  ?: 'unknown') . "\n"
                 . "  Time:     " . date('Y-m-d H:i:s T') . "\n\n"
                 . "If this was you, no action needed.\n"
                 . "If it wasn't, email support@sidebarbuddy.com.\n\n"
                 . "— Sidebar Buddy";
        resendMailText($email, $subject, $body);
    }
}

echo json_encode(['valid' => true]);
