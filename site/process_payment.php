<?php
/**
 * Sidebar Buddy — Payment Processing & License Key Generation
 *
 * Receives POST data after a successful PayPal client-side capture,
 * verifies the order server-side with the PayPal REST API,
 * generates a license key, saves it to the database, and returns it as JSON.
 */

require_once __DIR__ . '/private/secrets.php';
require_once __DIR__ . '/private/resend_mailer.php';

header('Content-Type: application/json');

if ($_SERVER['REQUEST_METHOD'] !== 'POST') {
    http_response_code(405);
    echo json_encode(['success' => false, 'error' => 'Method not allowed']);
    exit;
}

$input = json_decode(file_get_contents('php://input'), true);

if (!$input || empty($input['order_id']) || empty($input['email'])) {
    http_response_code(400);
    echo json_encode(['success' => false, 'error' => 'Missing required fields']);
    exit;
}

$orderId   = $input['order_id'];
$email     = filter_var($input['email'], FILTER_SANITIZE_EMAIL);
$payerName = isset($input['payer_name']) ? trim($input['payer_name']) : '';

if (!filter_var($email, FILTER_VALIDATE_EMAIL)) {
    http_response_code(400);
    echo json_encode(['success' => false, 'error' => 'Invalid email address']);
    exit;
}

// Verify order with PayPal before issuing any key.
$verified = verifyPayPalOrder($orderId);
if (!$verified) {
    http_response_code(402);
    echo json_encode(['success' => false, 'error' => 'Payment verification failed. Please contact support if you were charged.']);
    exit;
}

// Connect to database
try {
    $pdo = new PDO(
        'mysql:host=' . DB_HOST . ';dbname=' . DB_NAME . ';charset=utf8mb4',
        DB_USER, DB_PASS,
        [PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION, PDO::ATTR_DEFAULT_FETCH_MODE => PDO::FETCH_ASSOC]
    );
} catch (PDOException $e) {
    http_response_code(500);
    echo json_encode(['success' => false, 'error' => 'Database connection failed']);
    exit;
}

// Idempotency: return existing key if order was already processed
$stmt = $pdo->prepare('SELECT license_key FROM licenses WHERE order_id = ?');
$stmt->execute([$orderId]);
$existing = $stmt->fetch();
if ($existing) {
    echo json_encode(['success' => true, 'license_key' => $existing['license_key'], 'email' => $email]);
    exit;
}

$licenseKey = generateLicenseKey();

try {
    $pdo->prepare(
        'INSERT INTO licenses (order_id, email, payer_name, license_key, type, amount) VALUES (?, ?, ?, ?, ?, ?)'
    )->execute([$orderId, $email, $payerName, $licenseKey, 'paid', APP_PRICE]);
} catch (PDOException $e) {
    http_response_code(500);
    echo json_encode(['success' => false, 'error' => 'Failed to save license']);
    exit;
}

// Backup log
$logEntry = date('Y-m-d H:i:s') . " | Order: {$orderId} | Email: {$email} | Key: {$licenseKey}\n";
file_put_contents(__DIR__ . '/logs/transactions.log', $logEntry, FILE_APPEND | LOCK_EX);

sendBuyerEmail($email, $payerName, $licenseKey);
sendOwnerNotification($email, $payerName, $orderId, $licenseKey);

echo json_encode(['success' => true, 'license_key' => $licenseKey, 'email' => $email]);


// ─────────────────────────────────────────────────────────────────────────────

function verifyPayPalOrder(string $orderId): bool {
    $ch = curl_init('https://api-m.paypal.com/v1/oauth2/token');
    curl_setopt_array($ch, [
        CURLOPT_POST           => true,
        CURLOPT_USERPWD        => PAYPAL_CLIENT_ID . ':' . PAYPAL_CLIENT_SECRET,
        CURLOPT_POSTFIELDS     => 'grant_type=client_credentials',
        CURLOPT_RETURNTRANSFER => true,
        CURLOPT_TIMEOUT        => 15,
    ]);
    $tokenResp = json_decode(curl_exec($ch), true);
    $tokenErr  = curl_error($ch);
    curl_close($ch);

    if ($tokenErr || empty($tokenResp['access_token'])) {
        error_log('SB PayPal token error: ' . ($tokenErr ?: json_encode($tokenResp)));
        return false;
    }

    $ch = curl_init('https://api-m.paypal.com/v2/checkout/orders/' . urlencode($orderId));
    curl_setopt_array($ch, [
        CURLOPT_RETURNTRANSFER => true,
        CURLOPT_TIMEOUT        => 15,
        CURLOPT_HTTPHEADER     => ['Authorization: Bearer ' . $tokenResp['access_token']],
    ]);
    $orderResp = json_decode(curl_exec($ch), true);
    $orderErr  = curl_error($ch);
    curl_close($ch);

    if ($orderErr || empty($orderResp['status'])) {
        error_log('SB PayPal order fetch error: ' . ($orderErr ?: json_encode($orderResp)));
        return false;
    }

    if ($orderResp['status'] !== 'COMPLETED') return false;

    $amount = (float)($orderResp['purchase_units'][0]['amount']['value'] ?? 0);
    return $amount >= APP_PRICE;
}

function generateLicenseKey(): string {
    $chars    = 'ABCDEFGHJKLMNPQRSTUVWXYZ23456789'; // Omit 0, O, 1, I
    $segments = [];
    for ($i = 0; $i < 4; $i++) {
        $segment = '';
        for ($j = 0; $j < 4; $j++) $segment .= $chars[random_int(0, strlen($chars) - 1)];
        $segments[] = $segment;
    }
    return 'SB-' . implode('-', $segments);
}

function sendBuyerEmail(string $email, string $name, string $licenseKey): void {
    $firstName = $name ? explode(' ', $name)[0] : 'there';
    $subject   = 'Your Sidebar Buddy License Key';

    $html = '<!DOCTYPE html><html><head><meta charset="UTF-8"></head><body style="margin:0;padding:0;background:#0f0f1a;font-family:\'Inter\',Arial,sans-serif;">
<table width="100%" cellpadding="0" cellspacing="0" style="background:#0f0f1a;padding:40px 20px;">
  <tr><td align="center">
    <table width="560" cellpadding="0" cellspacing="0" style="background:#1c1c2e;border-radius:12px;overflow:hidden;max-width:560px;">
      <tr><td style="padding:28px 36px 20px;border-bottom:1px solid #2a2a3e;">
        <span style="color:#e0e0e0;font-size:20px;font-weight:700;letter-spacing:-0.02em;">&#9632; Sidebar Buddy</span>
      </td></tr>
      <tr><td style="padding:32px 36px;">
        <h1 style="color:#e0e0e0;font-size:22px;font-weight:600;margin:0 0 8px;">Thanks for your purchase, ' . htmlspecialchars($firstName) . '!</h1>
        <p style="color:#8888a0;font-size:15px;margin:0 0 28px;">Here is your Sidebar Buddy license key. Keep this email safe.</p>

        <div style="background:#0f0f1a;border:1px solid #2a2a3e;border-radius:8px;padding:20px 24px;margin-bottom:20px;">
          <p style="color:#8888a0;font-size:11px;text-transform:uppercase;letter-spacing:0.1em;margin:0 0 8px;">Your License Key</p>
          <code style="color:#4f8ef7;font-size:20px;letter-spacing:0.12em;font-family:\'Courier New\',monospace;word-break:break-all;">' . htmlspecialchars($licenseKey) . '</code>
        </div>

        <div style="background:#0f0f1a;border:1px solid #2a2a3e;border-radius:8px;padding:16px 24px;margin-bottom:28px;">
          <p style="color:#8888a0;font-size:11px;text-transform:uppercase;letter-spacing:0.1em;margin:0 0 6px;">Registered To</p>
          <p style="color:#e0e0e0;font-size:15px;margin:0;">' . htmlspecialchars($email) . '</p>
        </div>

        <p style="color:#8888a0;font-size:14px;line-height:1.6;margin:0 0 24px;">
          To activate: open Sidebar Buddy &rarr; click <strong style="color:#c0c0d0;">Settings</strong> &rarr; <strong style="color:#c0c0d0;">License</strong> &rarr; enter your email and paste the key above.
        </p>

        <a href="' . SITE_URL . '" style="display:inline-block;background:#4f8ef7;color:#ffffff;text-decoration:none;font-size:15px;font-weight:600;padding:13px 28px;border-radius:8px;">Visit Sidebar Buddy</a>
      </td></tr>
      <tr><td style="padding:18px 36px 24px;border-top:1px solid #2a2a3e;">
        <p style="color:#505060;font-size:13px;margin:0;">Questions? <a href="mailto:support@sidebarbuddy.com" style="color:#4f8ef7;text-decoration:none;">support@sidebarbuddy.com</a></p>
      </td></tr>
    </table>
  </td></tr>
</table>
</body></html>';

    resendMail($email, $subject, $html);
}

function sendOwnerNotification(string $email, string $name, string $orderId, string $licenseKey): void {
    $subject = 'New Sidebar Buddy Sale — $' . number_format(APP_PRICE, 2) . ' USD';
    $body    = "New sale!\n\n"
             . "Buyer:    {$name} <{$email}>\n"
             . "Order ID: {$orderId}\n"
             . "Key:      {$licenseKey}\n"
             . "Amount:   $" . number_format(APP_PRICE, 2) . " USD\n"
             . "Time:     " . date('Y-m-d H:i:s') . " UTC\n";

    resendMailText(NOTIFY_EMAIL, $subject, $body);
}
