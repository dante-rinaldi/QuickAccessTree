<?php
/**
 * Sidebar Buddy — Trial Status Endpoint
 *
 * Called by the desktop app on launch to register the trial start date
 * server-side and get authoritative days remaining.
 *
 * POST body (JSON): { "device_id": "<40-char hex>", "mac_address": "aa:bb:cc:dd:ee:ff" }
 * Response:         { "trial_start_date": "ISO8601", "days_remaining": N, "trial_days": 15 }
 *               or  { "error": "..." }
 */

require_once __DIR__ . '/private/secrets.php';

header('Content-Type: application/json');

define('TRIAL_DAYS', 15);

if ($_SERVER['REQUEST_METHOD'] !== 'POST') {
    http_response_code(405);
    echo json_encode(['error' => 'Method not allowed']);
    exit;
}

$input       = json_decode(file_get_contents('php://input'), true);
$device_id   = trim($input['device_id']  ?? '');
$mac_raw     = trim($input['mac_address'] ?? '');
$mac_address = preg_match('/^([0-9a-f]{2}:){5}[0-9a-f]{2}$/i', $mac_raw) ? strtolower($mac_raw) : null;

if (!preg_match('/^[0-9a-f]{32,64}$/', $device_id)) {
    http_response_code(400);
    echo json_encode(['error' => 'Invalid device_id']);
    exit;
}

// Rate limit: 30 requests per IP per hour
$ip    = trim(explode(',', $_SERVER['HTTP_X_FORWARDED_FOR'] ?? $_SERVER['REMOTE_ADDR'] ?? 'unknown')[0]);
$rlKey = sys_get_temp_dir() . '/sb_trial_rl_' . md5($ip) . '.json';
$now   = time();
$rl    = file_exists($rlKey) ? (json_decode(file_get_contents($rlKey), true) ?: []) : [];
$rl    = array_values(array_filter($rl, fn($t) => $now - $t < 3600));
if (count($rl) >= 30) {
    http_response_code(429);
    echo json_encode(['error' => 'Too many requests']);
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
    echo json_encode(['error' => 'Server error']);
    exit;
}

$existingRow = $pdo->prepare('SELECT id FROM trial_devices WHERE device_id = ? LIMIT 1');
$existingRow->execute([$device_id]);
$is_new = !$existingRow->fetch();

// IP geolocation (best-effort)
$country = $city = null;
if ($ip && $ip !== '127.0.0.1' && $ip !== '::1') {
    $geo = @file_get_contents("http://ip-api.com/json/{$ip}?fields=country,city");
    if ($geo) { $gd = json_decode($geo, true); $country = $gd['country'] ?? null; $city = $gd['city'] ?? null; }
}

$pdo->prepare(
    'INSERT INTO trial_devices (device_id, mac_address, ip_address, country, city)
         VALUES (?, ?, ?, ?, ?)
     ON DUPLICATE KEY UPDATE
         last_seen    = NOW(),
         launch_count = launch_count + 1,
         ip_address   = VALUES(ip_address),
         country      = COALESCE(country, VALUES(country)),
         city         = COALESCE(city,    VALUES(city)),
         mac_address  = COALESCE(mac_address, VALUES(mac_address))'
)->execute([$device_id, $mac_address, $ip, $country, $city]);

$row = $pdo->prepare('SELECT first_seen FROM trial_devices WHERE device_id = ? LIMIT 1');
$row->execute([$device_id]);
$data = $row->fetch();

if (!$data) { http_response_code(500); echo json_encode(['error' => 'Server error']); exit; }

// Notify on new install
if ($is_new) {
    $loc     = array_filter([$city, $country]);
    $subject = '[Sidebar Buddy] New Installation';
    $body    = "New Sidebar Buddy installation.\n\n"
             . "Device ID: {$device_id}\n"
             . "MAC:       " . ($mac_address ?? 'unknown') . "\n"
             . "IP:        {$ip}\n"
             . "Location:  " . ($loc ? implode(', ', $loc) : 'Unknown') . "\n"
             . "Time:      " . date('Y-m-d H:i:s T') . "\n";
    @mail(NOTIFY_EMAIL, $subject, $body, 'From: ' . FROM_EMAIL);
}

$first_seen     = new DateTimeImmutable($data['first_seen']);
$elapsed_days   = (int) $first_seen->diff(new DateTimeImmutable())->days;
$days_remaining = max(0, TRIAL_DAYS - $elapsed_days);

echo json_encode([
    'trial_start_date' => $first_seen->format('Y-m-d\TH:i:s'),
    'days_remaining'   => $days_remaining,
    'trial_days'       => TRIAL_DAYS,
]);
