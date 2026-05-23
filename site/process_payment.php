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
    $base = defined('SANDBOX_MODE') && SANDBOX_MODE
        ? 'https://api-m.sandbox.paypal.com'
        : 'https://api-m.paypal.com';

    $ch = curl_init($base . '/v1/oauth2/token');
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

    $ch = curl_init($base . '/v2/checkout/orders/' . urlencode($orderId));
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
    $logoUrl   = 'https://raw.githubusercontent.com/dante-rinaldi/QuickAccessTree/master/site/logo/logo_sideBarBuddy_forEmail.jpg';

    $html = '<!DOCTYPE html>
<html lang="en">
<head><meta charset="UTF-8"><meta name="viewport" content="width=device-width,initial-scale=1"></head>
<body style="margin:0;padding:0;background:#0b0b14;font-family:\'Segoe UI\',Arial,sans-serif;">

<table width="100%" cellpadding="0" cellspacing="0" style="background:#0b0b14;padding:40px 16px;">
<tr><td align="center">
<table width="580" cellpadding="0" cellspacing="0" style="max-width:580px;width:100%;">

  <!-- Logo header -->
  <tr><td align="center" style="padding-bottom:28px;">
    <img src="' . $logoUrl . '" alt="Sidebar Buddy" width="260" style="display:block;max-width:260px;height:auto;">
  </td></tr>

  <!-- Card -->
  <tr><td style="background:#13131f;border:1px solid #2a2a3e;border-radius:16px;overflow:hidden;">

    <!-- Accent bar -->
    <tr><td style="background:linear-gradient(90deg,#1a4a7a 0%,#0e639c 50%,#40c4ff 100%);height:4px;font-size:0;line-height:0;">&nbsp;</td></tr>

    <!-- Greeting -->
    <tr><td style="padding:36px 40px 0;">
      <h1 style="color:#f0f0f8;font-size:24px;font-weight:700;margin:0 0 10px;letter-spacing:-0.02em;">
        You\'re all set, ' . htmlspecialchars($firstName) . '! &#127881;
      </h1>
      <p style="color:#9090b8;font-size:15px;line-height:1.6;margin:0 0 32px;">
        Thanks for purchasing Sidebar Buddy. Your license key is below — keep this email somewhere safe.
      </p>
    </td></tr>

    <!-- License key block -->
    <tr><td style="padding:0 40px 24px;">
      <div style="background:#0b0b14;border:1px solid #2a2a3e;border-radius:10px;padding:24px 28px;">
        <p style="color:#6060a0;font-size:10px;font-weight:600;text-transform:uppercase;letter-spacing:0.12em;margin:0 0 12px;">Your License Key</p>
        <p style="color:#40c4ff;font-size:22px;font-weight:700;letter-spacing:0.15em;font-family:\'Courier New\',monospace;margin:0 0 16px;word-break:break-all;">' . htmlspecialchars($licenseKey) . '</p>
        <p style="color:#6060a0;font-size:10px;font-weight:600;text-transform:uppercase;letter-spacing:0.12em;margin:0 0 6px;">Registered To</p>
        <p style="color:#c0c0d8;font-size:14px;margin:0;">' . htmlspecialchars($email) . '</p>
      </div>
    </td></tr>

    <!-- How to activate -->
    <tr><td style="padding:0 40px 28px;">
      <p style="color:#6060a0;font-size:10px;font-weight:600;text-transform:uppercase;letter-spacing:0.12em;margin:0 0 14px;">How To Activate</p>
      <table cellpadding="0" cellspacing="0" width="100%">
        <tr>
          <td width="28" valign="top" style="color:#40c4ff;font-size:15px;font-weight:700;padding-top:1px;">1.</td>
          <td style="color:#9090b8;font-size:14px;line-height:1.6;padding-bottom:8px;">Open <strong style="color:#d0d0e8;">Sidebar Buddy</strong> and click the <strong style="color:#d0d0e8;">gear icon</strong> to open Settings.</td>
        </tr>
        <tr>
          <td width="28" valign="top" style="color:#40c4ff;font-size:15px;font-weight:700;padding-top:1px;">2.</td>
          <td style="color:#9090b8;font-size:14px;line-height:1.6;padding-bottom:8px;">Go to the <strong style="color:#d0d0e8;">License</strong> tab.</td>
        </tr>
        <tr>
          <td width="28" valign="top" style="color:#40c4ff;font-size:15px;font-weight:700;padding-top:1px;">3.</td>
          <td style="color:#9090b8;font-size:14px;line-height:1.6;">Enter your email address and paste the key above, then click <strong style="color:#d0d0e8;">Activate</strong>.</td>
        </tr>
      </table>
    </td></tr>

    <!-- CTA button -->
    <tr><td style="padding:0 40px 36px;">
      <a href="' . SITE_URL . '" style="display:inline-block;background:#0e639c;color:#ffffff;text-decoration:none;font-size:15px;font-weight:600;padding:14px 32px;border-radius:8px;letter-spacing:0.01em;">Visit sidebarbuddy.com</a>
    </td></tr>

    <!-- Footer -->
    <tr><td style="padding:20px 40px 24px;border-top:1px solid #1e1e30;">
      <p style="color:#404060;font-size:13px;margin:0;">
        Questions? Reply to this email or contact <a href="mailto:support@sidebarbuddy.com" style="color:#0e639c;text-decoration:none;">support@sidebarbuddy.com</a>
      </p>
    </td></tr>

  </td></tr>
  <!-- End card -->

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
