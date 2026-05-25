<?php
/**
 * Sidebar Buddy - My Account Dashboard
 *
 * Email-based lookup with 6-digit verification code authentication.
 * States: email entry → code verification → dashboard
 *
 * AJAX routes (POST ?action=...):
 *   send_code        - check email exists, send 6-digit code
 *   verify_code      - validate code, return license data
 *   change_email     - update email on license record
 *   get_activations  - list registered machines
 *   remove_activation - self-service remove one machine (one-time)
 *   request_transfer - submit admin-review transfer request
 */

session_start();
require_once __DIR__ . '/private/secrets.php';
require_once __DIR__ . '/private/resend_mailer.php';

// ── AJAX handlers ─────────────────────────────────────────────────────────
if (isset($_GET['action']) && $_SERVER['REQUEST_METHOD'] === 'POST') {
    header('Content-Type: application/json');
    $input = json_decode(file_get_contents('php://input'), true) ?? [];

    try {
        $pdo = new PDO('mysql:host='.DB_HOST.';dbname='.DB_NAME.';charset=utf8mb4',
            DB_USER, DB_PASS, [PDO::ATTR_ERRMODE=>PDO::ERRMODE_EXCEPTION, PDO::ATTR_DEFAULT_FETCH_MODE=>PDO::FETCH_ASSOC]);
    } catch (PDOException $e) {
        http_response_code(500);
        echo json_encode(['success'=>false,'error'=>'Database connection failed']);
        exit;
    }

    switch ($_GET['action']) {

        case 'send_code':
            $email = filter_var($input['email'] ?? '', FILTER_SANITIZE_EMAIL);
            if (!filter_var($email, FILTER_VALIDATE_EMAIL)) {
                echo json_encode(['success'=>false,'error'=>'Please enter a valid email address.']); exit;
            }
            // Rate limit: 5 sends per 15 min per session
            $now = time();
            $_SESSION['code_sends'] = array_values(array_filter($_SESSION['code_sends'] ?? [], fn($t) => $now - $t < 900));
            if (count($_SESSION['code_sends']) >= 5) {
                echo json_encode(['success'=>false,'error'=>'Too many attempts. Please try again in a few minutes.']); exit;
            }
            // Check license exists
            $stmt = $pdo->prepare('SELECT id FROM licenses WHERE email = ? AND status = ? LIMIT 1');
            $stmt->execute([$email, 'active']);
            if (!$stmt->fetch()) {
                echo json_encode(['success'=>false,'error'=>'No active license found for this email address.']); exit;
            }
            // Generate and store code
            $code = str_pad(random_int(0, 999999), 6, '0', STR_PAD_LEFT);
            $_SESSION['verify_email']   = $email;
            $_SESSION['verify_code']    = $code;
            $_SESSION['verify_expires'] = $now + 600;
            $_SESSION['code_attempts']  = 0;
            $_SESSION['code_sends'][]   = $now;

            $subject  = 'Sidebar Buddy - Your Verification Code';
            $logoUrl  = 'https://raw.githubusercontent.com/dante-rinaldi/QuickAccessTree/master/site/logo/logo_sideBarBuddy_forEmail.jpg';
            $codeHtml = '<!DOCTYPE html>
<html lang="en">
<head><meta charset="UTF-8"><meta name="viewport" content="width=device-width,initial-scale=1"></head>
<body style="margin:0;padding:0;background:#000000;font-family:\'Segoe UI\',Arial,sans-serif;" bgcolor="#000000">
<table width="100%" cellpadding="0" cellspacing="0" bgcolor="#000000" style="background:#000000;padding:40px 16px;">
<tr><td align="center">
<table width="580" cellpadding="0" cellspacing="0" style="max-width:580px;width:100%;">
  <tr><td align="center" bgcolor="#000000" style="background:#000000;padding:20px 0 28px;">
    <img src="' . $logoUrl . '" alt="Sidebar Buddy" width="260" style="display:block;max-width:260px;height:auto;">
  </td></tr>
  <tr><td style="background:#13131f;border:1px solid #2a2a3e;border-radius:16px;overflow:hidden;">
  <table width="100%" cellpadding="0" cellspacing="0">
    <tr><td style="background:linear-gradient(90deg,#1a4a7a 0%,#0e639c 50%,#40c4ff 100%);height:4px;font-size:0;line-height:0;">&nbsp;</td></tr>
    <tr><td style="padding:36px 40px 0;">
      <h1 style="color:#f0f0f8;font-size:24px;font-weight:700;margin:0 0 10px;letter-spacing:-0.02em;">Your verification code</h1>
      <p style="color:#9090b8;font-size:15px;line-height:1.6;margin:0 0 32px;">Use the code below to sign in to your Sidebar Buddy account. It expires in 10 minutes.</p>
    </td></tr>
    <tr><td style="padding:0 40px 32px;">
      <div style="background:#0b0b14;border:1px solid #2a2a3e;border-radius:10px;padding:28px;text-align:center;">
        <p style="color:#6060a0;font-size:10px;font-weight:600;text-transform:uppercase;letter-spacing:0.12em;margin:0 0 16px;">Verification Code</p>
        <p style="color:#40c4ff;font-size:36px;font-weight:700;letter-spacing:0.3em;font-family:\'Courier New\',monospace;margin:0;">' . htmlspecialchars($code) . '</p>
      </div>
    </td></tr>
    <tr><td style="padding:0 40px 36px;">
      <p style="color:#6060a0;font-size:13px;margin:0;">If you didn\'t request this, you can safely ignore this email - your account is secure.</p>
    </td></tr>
    <tr><td style="padding:20px 40px 24px;border-top:1px solid #1e1e30;">
      <p style="color:#404060;font-size:13px;margin:0;">Questions? <a href="https://sidebarbuddy.com/contact" style="color:#0e639c;text-decoration:none;">Contact support</a></p>
    </td></tr>
  </table>
  </td></tr>
</table>
</td></tr>
</table>
</body></html>';
            resendMail($email, $subject, $codeHtml);
            echo json_encode(['success'=>true]); exit;

        case 'verify_code':
            $code    = trim($input['code'] ?? '');
            $sessMail = $_SESSION['verify_email'] ?? null;
            $sessCode = $_SESSION['verify_code']  ?? null;
            $expires  = $_SESSION['verify_expires'] ?? 0;
            if (!$sessMail || !$sessCode) {
                echo json_encode(['success'=>false,'error'=>'No verification in progress. Please start over.']); exit;
            }
            if (time() > $expires) {
                unset($_SESSION['verify_code'], $_SESSION['verify_expires']);
                echo json_encode(['success'=>false,'error'=>'Code has expired. Please request a new one.']); exit;
            }
            if (($_SESSION['code_attempts'] ?? 0) >= 10) {
                unset($_SESSION['verify_code'], $_SESSION['verify_expires'], $_SESSION['verify_email'], $_SESSION['code_attempts']);
                echo json_encode(['success'=>false,'error'=>'Too many wrong attempts. Please request a new code.']); exit;
            }
            if (!hash_equals($sessCode, $code)) {
                $_SESSION['code_attempts'] = ($_SESSION['code_attempts'] ?? 0) + 1;
                echo json_encode(['success'=>false,'error'=>'Incorrect code. Please try again.']); exit;
            }
            $_SESSION['authenticated_email'] = $sessMail;
            $_SESSION['code_attempts'] = 0;
            unset($_SESSION['verify_code'], $_SESSION['verify_expires']);

            $stmt = $pdo->prepare('SELECT license_key, email, created_at FROM licenses WHERE email = ? LIMIT 1');
            $stmt->execute([$sessMail]);
            $lic = $stmt->fetch();
            echo json_encode(['success'=>true,'license'=>[
                'key'=>$lic['license_key'],'email'=>$lic['email'],
                'purchase_date'=>$lic['created_at'],'status'=>'Active'
            ]]); exit;

        case 'change_email':
            $auth = $_SESSION['authenticated_email'] ?? null;
            if (!$auth) { http_response_code(401); echo json_encode(['success'=>false,'error'=>'Not authenticated.']); exit; }
            $newEmail = filter_var($input['new_email'] ?? '', FILTER_SANITIZE_EMAIL);
            if (!filter_var($newEmail, FILTER_VALIDATE_EMAIL)) {
                echo json_encode(['success'=>false,'error'=>'Please enter a valid email address.']); exit;
            }
            $stmt = $pdo->prepare('SELECT id FROM licenses WHERE email = ? AND email != ? LIMIT 1');
            $stmt->execute([$newEmail, $auth]);
            if ($stmt->fetch()) {
                echo json_encode(['success'=>false,'error'=>'That email is already associated with another license.']); exit;
            }
            $pdo->prepare('UPDATE licenses SET email = ? WHERE email = ?')->execute([$newEmail, $auth]);
            $pdo->prepare('UPDATE license_activations SET email = ? WHERE email = ?')->execute([$newEmail, $auth]);
            $_SESSION['authenticated_email'] = $newEmail;
            echo json_encode(['success'=>true,'email'=>$newEmail]); exit;

        case 'get_activations':
            $auth = $_SESSION['authenticated_email'] ?? null;
            if (!$auth) { http_response_code(401); echo json_encode(['success'=>false,'error'=>'Not authenticated.']); exit; }
            $lic = $pdo->prepare('SELECT license_key FROM licenses WHERE email = ? LIMIT 1');
            $lic->execute([$auth]);
            $licRow = $lic->fetch();
            if (!$licRow) { echo json_encode(['success'=>false,'error'=>'License not found.']); exit; }
            $key = $licRow['license_key'];
            $acts = $pdo->prepare('SELECT device_id, mac_address, hostname, country, activated_at, last_seen FROM license_activations WHERE license_key = ? ORDER BY activated_at ASC');
            $acts->execute([$key]);
            $selfSvc = $pdo->prepare("SELECT COUNT(*) FROM activation_transfers WHERE email = ? AND type = 'self_service'");
            $selfSvc->execute([$auth]);
            $selfSvcUsed = (int)$selfSvc->fetchColumn() > 0;
            $pending = $pdo->prepare("SELECT COUNT(*) FROM activation_transfers WHERE email = ? AND type = 'transfer_request' AND status = 'pending'");
            $pending->execute([$auth]);
            echo json_encode(['success'=>true,'activations'=>$acts->fetchAll(),'self_service_used'=>$selfSvcUsed,'has_pending_request'=>(int)$pending->fetchColumn() > 0,'limit'=>2]); exit;

        case 'remove_activation':
            $auth = $_SESSION['authenticated_email'] ?? null;
            if (!$auth) { http_response_code(401); echo json_encode(['success'=>false,'error'=>'Not authenticated.']); exit; }
            $deviceId = trim($input['device_id'] ?? '');
            if (!preg_match('/^[0-9a-f]{32,64}$/', $deviceId)) {
                echo json_encode(['success'=>false,'error'=>'Invalid device.']); exit;
            }
            $licRow = $pdo->prepare('SELECT license_key FROM licenses WHERE email = ? LIMIT 1');
            $licRow->execute([$auth]);
            $lic = $licRow->fetch();
            if (!$lic) { echo json_encode(['success'=>false,'error'=>'License not found.']); exit; }
            $key = $lic['license_key'];
            $selfSvc = $pdo->prepare("SELECT COUNT(*) FROM activation_transfers WHERE email = ? AND type = 'self_service'");
            $selfSvc->execute([$auth]);
            if ((int)$selfSvc->fetchColumn() > 0) {
                echo json_encode(['success'=>false,'error'=>'You have already used your one free machine transfer. Submit a transfer request for additional removals.']); exit;
            }
            $actRow = $pdo->prepare('SELECT mac_address FROM license_activations WHERE license_key = ? AND device_id = ? LIMIT 1');
            $actRow->execute([$key, $deviceId]);
            $act = $actRow->fetch();
            if (!$act) { echo json_encode(['success'=>false,'error'=>'Activation not found.']); exit; }
            $pdo->prepare('DELETE FROM license_activations WHERE license_key = ? AND device_id = ?')->execute([$key, $deviceId]);
            $pdo->prepare("INSERT INTO activation_transfers (license_key, email, type, removed_device_id, removed_mac, status) VALUES (?, ?, 'self_service', ?, ?, 'completed')")
                ->execute([$key, $auth, $deviceId, $act['mac_address']]);
            resendMailText(NOTIFY_EMAIL, '[Sidebar Buddy] Machine Activation Removed',
                "Self-service machine removal.\n\nEmail: {$auth}\nKey: {$key}\nDevice: {$deviceId}\nMAC: " . ($act['mac_address'] ?? 'N/A') . "\nTime: " . date('Y-m-d H:i:s T'));
            echo json_encode(['success'=>true]); exit;

        case 'request_transfer':
            $auth = $_SESSION['authenticated_email'] ?? null;
            if (!$auth) { http_response_code(401); echo json_encode(['success'=>false,'error'=>'Not authenticated.']); exit; }
            $note = substr(trim($input['note'] ?? ''), 0, 1000);
            $licRow = $pdo->prepare('SELECT license_key FROM licenses WHERE email = ? LIMIT 1');
            $licRow->execute([$auth]);
            $lic = $licRow->fetch();
            if (!$lic) { echo json_encode(['success'=>false,'error'=>'License not found.']); exit; }
            $key = $lic['license_key'];
            $pending = $pdo->prepare("SELECT COUNT(*) FROM activation_transfers WHERE email = ? AND type = 'transfer_request' AND status = 'pending'");
            $pending->execute([$auth]);
            if ((int)$pending->fetchColumn() > 0) {
                echo json_encode(['success'=>false,'error'=>'You already have a pending transfer request.']); exit;
            }
            $pdo->prepare("INSERT INTO activation_transfers (license_key, email, type, request_note, status) VALUES (?, ?, 'transfer_request', ?, 'pending')")
                ->execute([$key, $auth, $note]);
            resendMailText(NOTIFY_EMAIL, '[Sidebar Buddy] Machine Transfer Request',
                "Transfer request submitted.\n\nEmail: {$auth}\nKey: {$key}\nNote: {$note}\nTime: " . date('Y-m-d H:i:s T'));
            echo json_encode(['success'=>true]); exit;

        default:
            http_response_code(400);
            echo json_encode(['success'=>false,'error'=>'Unknown action']); exit;
    }
}
?>
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8">
  <meta name="viewport" content="width=device-width, initial-scale=1.0">
  <title>My Account - Sidebar Buddy</title>
  <meta name="robots" content="noindex, follow">
  <link rel="stylesheet" href="style.css">
  <style>
    .acct-wrap { max-width: 620px; margin: 0 auto; padding: 56px 24px 80px; position: relative; z-index: 1; }
    .acct-card { background: #13131f; border: 1px solid #2a2a3e; border-radius: 14px; padding: 32px; margin-bottom: 20px; }
    .acct-card h2 { font-size: 1.5rem; font-weight: 700; color: var(--text); margin: 0 0 8px; }
    .acct-card h3 { font-size: 1rem; font-weight: 600; color: var(--text); margin: 0 0 6px; }
    .acct-subtitle { color: #9090b8; font-size: 0.9rem; line-height: 1.6; margin: 0 0 24px; }
    .acct-label { font-size: 0.68rem; font-weight: 600; text-transform: uppercase; letter-spacing: 0.1em; color: #5050a0; margin: 0 0 6px; }
    .acct-input { width: 100%; background: #0b0b14; border: 1px solid #2a2a3e; border-radius: 8px;
                  color: var(--text); font-size: 0.95rem; padding: 11px 14px; box-sizing: border-box;
                  font-family: inherit; margin-bottom: 12px; }
    .acct-input:focus { outline: none; border-color: var(--accent); }
    .acct-btn { width: 100%; background: #0e639c; color: #fff; border: none; border-radius: 8px;
                font-size: 0.95rem; font-weight: 600; padding: 13px; cursor: pointer; font-family: inherit; }
    .acct-btn:hover { background: #1177bb; }
    .acct-btn:disabled { opacity: 0.5; cursor: default; }
    .acct-btn-secondary { background: transparent; border: 1px solid #2a2a3e; color: #9090b8; }
    .acct-btn-secondary:hover { border-color: #4a4a6e; color: var(--text); background: transparent; }
    .acct-error { color: #ff6b6b; font-size: 0.85rem; margin: 4px 0 0; min-height: 20px; }
    .acct-success { color: #40c4ff; font-size: 0.85rem; margin: 4px 0 0; }
    .acct-hidden { display: none !important; }
    .acct-key-block { background: #0b0b14; border: 1px solid #2a2a3e; border-radius: 8px; padding: 18px 20px; margin-bottom: 16px; }
    .acct-key-value { font-family: 'Courier New', monospace; font-size: 1.2rem; font-weight: 700;
                      color: #40c4ff; letter-spacing: 0.12em; word-break: break-all; margin: 4px 0 12px; }
    .acct-field-row { display: flex; gap: 20px; margin-bottom: 16px; }
    .acct-field { flex: 1; }
    .acct-field-value { color: var(--text); font-size: 0.95rem; margin-top: 4px; }
    .acct-badge { display: inline-block; background: #0e3a1a; color: #40e080; font-size: 0.78rem;
                  font-weight: 600; padding: 3px 10px; border-radius: 20px; }
    .machine-row { display: flex; justify-content: space-between; align-items: center;
                   background: #0b0b14; border: 1px solid #2a2a3e; border-radius: 8px;
                   padding: 12px 16px; margin-bottom: 10px; }
    .machine-name { font-weight: 600; font-size: 0.9rem; color: var(--text); }
    .machine-meta { font-size: 0.75rem; color: #6060a0; margin-top: 3px; }
    .machine-remove { background: transparent; border: 1px solid #3a2020; color: #f87171;
                      font-size: 0.75rem; padding: 4px 12px; border-radius: 6px; cursor: pointer;
                      white-space: nowrap; margin-left: 12px; font-family: inherit; }
    .machine-remove:hover { background: #2a1010; }
    .acct-slots { font-size: 0.75rem; color: #6060a0; margin-top: 8px; }
    .acct-link { color: #40c4ff; font-size: 0.85rem; text-decoration: none; display: inline-block; margin-top: 12px; }
    .acct-link:hover { text-decoration: underline; }
    .acct-dl-row { display: flex; gap: 10px; }
    .acct-dl-row .acct-btn { flex: 1; }
    textarea.acct-input { resize: vertical; min-height: 80px; }
  </style>
</head>
<body>
  <nav>
    <div class="nav-inner">
      <div class="logo">
        <img src="logo/logo_sideBarBuddyIcon_Horiz.webp" alt="Sidebar Buddy" style="height:32px;width:auto;display:block;">
      </div>
      <div class="nav-links">
        <a href="/">Home</a>
        <a href="/contact">Support</a>
      </div>
    </div>
  </nav>

  <div class="acct-wrap">

    <!-- State 1: Email entry -->
    <div class="acct-card" id="emailCard">
      <h2>My Account</h2>
      <p class="acct-subtitle">Enter the email you used to purchase Sidebar Buddy and we'll send you a verification code.</p>
      <p class="acct-label">Email address</p>
      <input type="email" id="emailInput" class="acct-input" placeholder="you@example.com" autocomplete="email">
      <button class="acct-btn" id="sendCodeBtn" onclick="sendCode()">Send Verification Code</button>
      <p class="acct-error" id="emailError"></p>
    </div>

    <!-- State 2: Code entry -->
    <div class="acct-card acct-hidden" id="codeCard">
      <h2>Check Your Email</h2>
      <p class="acct-subtitle">Enter the 6-digit code sent to <strong id="codeEmailLabel" style="color:var(--text)"></strong>. Check your spam folder if you don't see it.</p>
      <p class="acct-label">Verification code</p>
      <input type="text" id="codeInput" class="acct-input" placeholder="000000" maxlength="6" inputmode="numeric" autocomplete="one-time-code">
      <button class="acct-btn" id="verifyBtn" onclick="verifyCode()">Verify</button>
      <p class="acct-error" id="codeError"></p>
      <a href="#" class="acct-link" onclick="sendCode();return false;">Resend code</a>
    </div>

    <!-- State 3: Dashboard -->
    <div id="dashboard" class="acct-hidden">

      <!-- License details -->
      <div class="acct-card">
        <h3>License Details</h3>
        <div class="acct-key-block">
          <p class="acct-label">License Key</p>
          <div class="acct-key-value" id="licKey"></div>
          <button class="acct-btn acct-btn-secondary" style="width:auto;padding:6px 18px;font-size:0.8rem;" onclick="copyKey()">Copy Key</button>
        </div>
        <div class="acct-field-row">
          <div class="acct-field">
            <p class="acct-label">Purchase Date</p>
            <p class="acct-field-value" id="licDate"></p>
          </div>
          <div class="acct-field">
            <p class="acct-label">Status</p>
            <span class="acct-badge">Active</span>
          </div>
        </div>
      </div>

      <!-- Registered machines -->
      <div class="acct-card" id="machinesCard">
        <h3>Registered Machines</h3>
        <p class="acct-subtitle">Your license allows activation on up to 2 computers.</p>
        <div id="machinesList"></div>
        <div id="transferSection" class="acct-hidden" style="margin-top:16px;">
          <p class="acct-subtitle">You've used your one free machine transfer. To free another slot, submit a request - we'll review it within 1 business day.</p>
          <textarea id="transferNote" class="acct-input" placeholder="Briefly describe why you need to free a slot (e.g. old laptop died, replaced my desktop)…"></textarea>
          <button class="acct-btn" id="transferBtn" onclick="submitTransfer()">Submit Transfer Request</button>
          <p class="acct-error" id="transferError"></p>
          <p class="acct-success acct-hidden" id="transferSuccess">Request submitted! We'll email you when it's approved.</p>
        </div>
      </div>

      <!-- Change email -->
      <div class="acct-card">
        <h3>Change Email</h3>
        <p class="acct-subtitle">Update the email address associated with your license.</p>
        <p class="acct-label">New email address</p>
        <input type="email" id="newEmailInput" class="acct-input" placeholder="newemail@example.com">
        <button class="acct-btn" id="changeEmailBtn" onclick="changeEmail()">Update Email</button>
        <p class="acct-error" id="changeEmailError"></p>
        <p class="acct-success acct-hidden" id="changeEmailSuccess">Email updated successfully.</p>
      </div>

      <!-- Download -->
      <div class="acct-card">
        <h3>Download Sidebar Buddy</h3>
        <p class="acct-subtitle">Get the latest version for Windows.</p>
        <div class="acct-dl-row">
          <a href="/download" class="acct-btn" style="text-align:center;text-decoration:none;display:flex;align-items:center;justify-content:center;">Download for Windows</a>
        </div>
      </div>

      <!-- Support -->
      <div class="acct-card">
        <h3>Support</h3>
        <p class="acct-subtitle">Lost your key? Need to move to a new PC? We're here to help.</p>
        <a href="/contact" class="acct-btn" style="text-align:center;text-decoration:none;display:flex;align-items:center;justify-content:center;">Contact Support</a>
      </div>

      <a href="#" class="acct-link" onclick="resetToEmail();return false;">← Look up a different email</a>
    </div>

  </div>

<script>
var _currentEmail = '';

function busy(btnId, yes) {
  var b = document.getElementById(btnId);
  b.disabled = yes;
  if (yes) { b.dataset.orig = b.textContent; b.textContent = 'Please wait…'; }
  else { b.textContent = b.dataset.orig || b.textContent; }
}

function post(action, data) {
  return fetch('account.php?action=' + action, {
    method: 'POST', headers: {'Content-Type':'application/json'}, body: JSON.stringify(data)
  }).then(function(r){ return r.json(); });
}

function sendCode() {
  var email = document.getElementById('emailInput').value.trim();
  if (!email) return;
  document.getElementById('emailError').textContent = '';
  busy('sendCodeBtn', true);
  post('send_code', {email: email}).then(function(d) {
    busy('sendCodeBtn', false);
    if (d.success) {
      _currentEmail = email;
      document.getElementById('codeEmailLabel').textContent = email;
      show('codeCard'); hide('emailCard');
      document.getElementById('codeInput').focus();
    } else {
      document.getElementById('emailError').textContent = d.error;
    }
  }).catch(function(){ busy('sendCodeBtn', false); document.getElementById('emailError').textContent = 'Something went wrong. Please try again.'; });
}

function verifyCode() {
  var code = document.getElementById('codeInput').value.trim();
  if (!code) return;
  document.getElementById('codeError').textContent = '';
  busy('verifyBtn', true);
  post('verify_code', {code: code}).then(function(d) {
    busy('verifyBtn', false);
    if (d.success) { showDashboard(d.license); }
    else { document.getElementById('codeError').textContent = d.error; }
  }).catch(function(){ busy('verifyBtn', false); document.getElementById('codeError').textContent = 'Something went wrong. Please try again.'; });
}

function showDashboard(lic) {
  hide('codeCard'); hide('emailCard'); show('dashboard');
  document.getElementById('licKey').textContent  = lic.key;
  document.getElementById('licDate').textContent = formatDate(lic.purchase_date);
  document.getElementById('newEmailInput').value = lic.email;
  loadActivations();
}

function copyKey() {
  navigator.clipboard.writeText(document.getElementById('licKey').textContent).then(function() {
    var b = document.querySelector('.acct-key-block .acct-btn');
    var orig = b.textContent; b.textContent = 'Copied!';
    setTimeout(function(){ b.textContent = orig; }, 2000);
  });
}

function changeEmail() {
  var ne = document.getElementById('newEmailInput').value.trim();
  if (!ne) return;
  document.getElementById('changeEmailError').textContent = '';
  hide('changeEmailSuccess');
  busy('changeEmailBtn', true);
  post('change_email', {new_email: ne}).then(function(d) {
    busy('changeEmailBtn', false);
    if (d.success) { show('changeEmailSuccess'); _currentEmail = d.email; }
    else { document.getElementById('changeEmailError').textContent = d.error; }
  }).catch(function(){ busy('changeEmailBtn', false); document.getElementById('changeEmailError').textContent = 'Something went wrong.'; });
}

function loadActivations() {
  post('get_activations', {}).then(function(d) {
    if (!d.success) return;
    renderMachines(d);
  });
}

function renderMachines(d) {
  var list = document.getElementById('machinesList');
  var acts = d.activations || [];
  if (!acts.length) { list.innerHTML = '<p class="acct-subtitle">No machines registered yet.</p>'; return; }
  var html = '';
  acts.forEach(function(a, i) {
    var name = a.hostname || ('Machine ' + (i+1));
    var meta = [a.mac_address, a.country, a.last_seen ? 'Last seen ' + formatDate(a.last_seen) : ''].filter(Boolean).join(' · ');
    html += '<div class="machine-row"><div><div class="machine-name">' + esc(name) + '</div><div class="machine-meta">' + esc(meta) + '</div></div>';
    if (!d.self_service_used)
      html += '<button class="machine-remove" onclick="removeActivation(\'' + esc(a.device_id) + '\')">Remove</button>';
    html += '</div>';
  });
  var left = d.self_service_used ? 0 : 1;
  html += '<p class="acct-slots">' + acts.length + ' of 2 slots used &nbsp;·&nbsp; ' + left + ' free transfer' + (left!==1?'s':'') + ' remaining</p>';
  list.innerHTML = html;
  if (d.has_pending_request) {
    document.getElementById('transferSection').innerHTML = '<p class="acct-subtitle" style="color:#40c4ff;">✓ Transfer request submitted - we\'ll email you when it\'s approved.</p>';
    show('transferSection');
  } else if (d.self_service_used) {
    show('transferSection');
  }
}

function removeActivation(deviceId) {
  if (!confirm('Remove this machine from your license? It will need to re-activate before it can run Sidebar Buddy again.')) return;
  post('remove_activation', {device_id: deviceId}).then(function(d) {
    if (d.success) loadActivations();
    else alert(d.error || 'Could not remove activation.');
  }).catch(function(){ alert('Something went wrong. Please try again.'); });
}

function submitTransfer() {
  var note = document.getElementById('transferNote').value.trim();
  document.getElementById('transferError').textContent = '';
  hide('transferSuccess');
  busy('transferBtn', true);
  post('request_transfer', {note: note}).then(function(d) {
    busy('transferBtn', false);
    if (d.success) { show('transferSuccess'); document.getElementById('transferBtn').disabled = true; }
    else { document.getElementById('transferError').textContent = d.error || 'Could not submit request.'; }
  }).catch(function(){ busy('transferBtn', false); document.getElementById('transferError').textContent = 'Something went wrong.'; });
}

function resetToEmail() {
  hide('dashboard'); hide('codeCard'); show('emailCard');
  document.getElementById('emailInput').value = '';
  document.getElementById('codeInput').value  = '';
  document.getElementById('emailError').textContent = '';
  document.getElementById('codeError').textContent  = '';
  document.getElementById('emailInput').focus();
}

function formatDate(s) {
  if (!s) return '';
  return new Date(s).toLocaleDateString('en-US', {year:'numeric',month:'long',day:'numeric'});
}
function esc(s) {
  return String(s).replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/"/g,'&quot;');
}
function show(id) { document.getElementById(id).classList.remove('acct-hidden'); }
function hide(id) { document.getElementById(id).classList.add('acct-hidden'); }

document.getElementById('emailInput').addEventListener('keydown', function(e){ if(e.key==='Enter') sendCode(); });
document.getElementById('codeInput').addEventListener('keydown',  function(e){ if(e.key==='Enter') verifyCode(); });
</script>
</body>
</html>
