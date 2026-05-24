<?php
/**
 * Sidebar Buddy — Admin Panel
 * Password-protected. ADMIN_PASSWORD defined in private/secrets.php.
 */

session_start();
require_once __DIR__ . '/../private/secrets.php';
require_once __DIR__ . '/../private/resend_mailer.php';

// ── Auth ──────────────────────────────────────────────────────────────────────

$loginError = '';
if (isset($_POST['password'])) {
    if ($_POST['password'] === ADMIN_PASSWORD) {
        $_SESSION['sb_admin'] = true;
    } else {
        $loginError = 'Wrong password.';
    }
}
if (isset($_GET['logout'])) {
    session_destroy();
    header('Location: index.php');
    exit;
}

if (empty($_SESSION['sb_admin'])) {
    // Login screen
    ?><!DOCTYPE html>
<html lang="en">
<head><meta charset="UTF-8"><title>Admin — Sidebar Buddy</title>
<style>
  body{margin:0;font-family:'Segoe UI',sans-serif;background:#0d0d10;color:#e0e0e0;display:flex;align-items:center;justify-content:center;height:100vh;}
  .box{background:#13131a;border:1px solid #2a2a3e;border-radius:10px;padding:36px 40px;width:320px;}
  h2{margin:0 0 24px;font-size:18px;color:#9090b0;}
  input{width:100%;box-sizing:border-box;background:#1c1c2e;border:1px solid #2a2a3e;color:#e0e0e0;padding:9px 12px;border-radius:6px;font-size:14px;margin-bottom:12px;}
  button{width:100%;background:#0e639c;color:#fff;border:none;border-radius:6px;padding:10px;font-size:14px;font-weight:600;cursor:pointer;}
  button:hover{background:#1177bb;}
  .err{color:#ff6b6b;font-size:13px;margin-bottom:10px;}
</style>
</head>
<body>
<div class="box">
  <h2>&#9632; Sidebar Buddy Admin</h2>
  <?php if ($loginError): ?><p class="err"><?= htmlspecialchars($loginError) ?></p><?php endif; ?>
  <form method="POST">
    <input type="password" name="password" placeholder="Password" autofocus>
    <button type="submit">Sign In</button>
  </form>
</div>
</body></html>
<?php
    exit;
}

// ── Database ──────────────────────────────────────────────────────────────────

try {
    $pdo = new PDO(
        'mysql:host=' . DB_HOST . ';dbname=' . DB_NAME . ';charset=utf8mb4',
        DB_USER, DB_PASS,
        [PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION, PDO::ATTR_DEFAULT_FETCH_MODE => PDO::FETCH_ASSOC]
    );
} catch (PDOException $e) {
    die('<p style="color:red;font-family:sans-serif;padding:40px">DB connection failed: ' . htmlspecialchars($e->getMessage()) . '</p>');
}

// Ensure downloads table exists
$pdo->exec("CREATE TABLE IF NOT EXISTS downloads (
    id INT AUTO_INCREMENT PRIMARY KEY,
    ip_address VARCHAR(45), country VARCHAR(100), city VARCHAR(100),
    region VARCHAR(100), user_agent VARCHAR(500),
    clicked_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4");

// Ensure notify_list table exists
$pdo->exec("CREATE TABLE IF NOT EXISTS notify_list (
    id         INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
    email      VARCHAR(255) NOT NULL UNIQUE,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4");

// CSV export
if (isset($_GET['export']) && $_GET['export'] === 'notify') {
    header('Content-Type: text/csv');
    header('Content-Disposition: attachment; filename="sb_notify_list_' . date('Y-m-d') . '.csv"');
    $out = fopen('php://output', 'w');
    fputcsv($out, ['Email', 'Signed Up']);
    foreach ($pdo->query('SELECT email, created_at FROM notify_list ORDER BY created_at DESC')->fetchAll() as $row) {
        fputcsv($out, [$row['email'], $row['created_at']]);
    }
    fclose($out);
    exit;
}

// ── Actions ───────────────────────────────────────────────────────────────────

$message = '';

// Grant comp license
if ($_SERVER['REQUEST_METHOD'] === 'POST' && isset($_POST['comp_email'])) {
    $compEmail = filter_var(trim($_POST['comp_email']), FILTER_SANITIZE_EMAIL);
    $compNote  = substr(trim($_POST['comp_note'] ?? ''), 0, 255);
    if (filter_var($compEmail, FILTER_VALIDATE_EMAIL)) {
        // Generate key
        $chars = 'ABCDEFGHJKLMNPQRSTUVWXYZ23456789';
        $segs  = [];
        for ($i = 0; $i < 4; $i++) {
            $s = '';
            for ($j = 0; $j < 4; $j++) $s .= $chars[random_int(0, strlen($chars) - 1)];
            $segs[] = $s;
        }
        $compKey = 'SB-' . implode('-', $segs);
        try {
            $pdo->prepare(
                'INSERT INTO licenses (order_id, email, payer_name, license_key, type, amount)
                 VALUES (?, ?, ?, ?, ?, ?)'
            )->execute(['COMP-' . time(), $compEmail, $compNote ?: 'Comp', $compKey, 'comp', 0]);

            // Email the key to the recipient
            $subject = 'Your Sidebar Buddy License Key';
            $logoUrl = 'https://raw.githubusercontent.com/dante-rinaldi/QuickAccessTree/master/site/logo/logo_sideBarBuddy_forEmail.jpg';
            $compHtml = '<!DOCTYPE html>
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
      <h1 style="color:#f0f0f8;font-size:24px;font-weight:700;margin:0 0 10px;letter-spacing:-0.02em;">You\'ve got a complimentary license!</h1>
      <p style="color:#9090b8;font-size:15px;line-height:1.6;margin:0 0 32px;">Here is your Sidebar Buddy license key, courtesy of Inferno Creative Studio. Keep this email somewhere safe.</p>
    </td></tr>
    <tr><td style="padding:0 40px 24px;">
      <div style="background:#0b0b14;border:1px solid #2a2a3e;border-radius:10px;padding:24px 28px;">
        <p style="color:#6060a0;font-size:10px;font-weight:600;text-transform:uppercase;letter-spacing:0.12em;margin:0 0 12px;">Your License Key</p>
        <p style="color:#40c4ff;font-size:22px;font-weight:700;letter-spacing:0.15em;font-family:\'Courier New\',monospace;margin:0 0 16px;word-break:break-all;">' . htmlspecialchars($compKey) . '</p>
        <p style="color:#6060a0;font-size:10px;font-weight:600;text-transform:uppercase;letter-spacing:0.12em;margin:0 0 6px;">Registered To</p>
        <p style="color:#c0c0d8;font-size:14px;margin:0;">' . htmlspecialchars($compEmail) . '</p>
      </div>
    </td></tr>
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
          <td style="color:#9090b8;font-size:14px;line-height:1.6;">Enter your email and paste the key above, then click <strong style="color:#d0d0e8;">Activate</strong>.</td>
        </tr>
      </table>
    </td></tr>
    <tr><td style="padding:0 40px 36px;">
      <table cellpadding="0" cellspacing="0"><tr>
        <td style="padding-right:12px;"><a href="https://sidebarbuddy.com/download" style="display:inline-block;background:#0e639c;color:#ffffff;text-decoration:none;font-size:15px;font-weight:600;padding:14px 32px;border-radius:8px;letter-spacing:0.01em;">Download for Windows</a></td>
        <td><a href="https://sidebarbuddy.com" style="display:inline-block;background:transparent;border:1px solid #2a2a3e;color:#9090b8;text-decoration:none;font-size:15px;font-weight:600;padding:13px 24px;border-radius:8px;letter-spacing:0.01em;">Visit sidebarbuddy.com</a></td>
      </tr></table>
    </td></tr>
    <tr><td style="padding:20px 40px 24px;border-top:1px solid #1e1e30;">
      <p style="color:#404060;font-size:13px;margin:0;">Questions? Reply to this email or <a href="https://sidebarbuddy.com/contact" style="color:#0e639c;text-decoration:none;">contact support</a></p>
    </td></tr>
  </table>
  </td></tr>
</table>
</td></tr>
</table>
</body></html>';
            resendMail($compEmail, $subject, $compHtml);

            $message = "Comp key {$compKey} granted to {$compEmail} and emailed.";
        } catch (PDOException $ex) {
            $message = 'Error: ' . $ex->getMessage();
        }
    } else {
        $message = 'Invalid email address.';
    }
}

// Revoke license
if ($_SERVER['REQUEST_METHOD'] === 'POST' && isset($_POST['revoke_key'])) {
    $revokeKey = trim($_POST['revoke_key']);
    $pdo->prepare('DELETE FROM licenses WHERE license_key = ?')->execute([$revokeKey]);
    $pdo->prepare('DELETE FROM license_activations WHERE license_key = ?')->execute([$revokeKey]);
    $message = "License {$revokeKey} revoked.";
}

// ── Stats ─────────────────────────────────────────────────────────────────────

$totalLicenses  = (int)$pdo->query('SELECT COUNT(*) FROM licenses')->fetchColumn();
$paidLicenses   = (int)$pdo->query("SELECT COUNT(*) FROM licenses WHERE type='paid'")->fetchColumn();
$compLicenses   = (int)$pdo->query("SELECT COUNT(*) FROM licenses WHERE type='comp'")->fetchColumn();
$totalTrials    = (int)$pdo->query('SELECT COUNT(*) FROM trial_devices')->fetchColumn();
$totalDownloads = (int)$pdo->query('SELECT COUNT(*) FROM downloads')->fetchColumn();
$revenue        = (float)$pdo->query('SELECT COALESCE(SUM(amount),0) FROM licenses')->fetchColumn();
$totalNotify    = (int)$pdo->query('SELECT COUNT(*) FROM notify_list')->fetchColumn();

// ── Data ──────────────────────────────────────────────────────────────────────

$tab = $_GET['tab'] ?? 'licenses';

$notifyList = $pdo->query('SELECT email, created_at FROM notify_list ORDER BY created_at DESC')->fetchAll();

$licenses = $pdo->query(
    'SELECT l.*, COUNT(a.id) as activations
     FROM licenses l
     LEFT JOIN license_activations a ON a.license_key = l.license_key
     GROUP BY l.id
     ORDER BY l.created_at DESC
     LIMIT 200'
)->fetchAll();

$trials = $pdo->query(
    'SELECT * FROM trial_devices ORDER BY last_seen DESC LIMIT 200'
)->fetchAll();

$downloads = $pdo->query(
    'SELECT * FROM downloads ORDER BY clicked_at DESC LIMIT 200'
)->fetchAll();

// Pre-fetch activation details for visible licenses (for expandable rows)
$activationDetails = [];
if ($tab === 'licenses' && $licenses) {
    $keys         = array_column($licenses, 'license_key');
    $placeholders = implode(',', array_fill(0, count($keys), '?'));
    $stmt = $pdo->prepare(
        "SELECT * FROM license_activations WHERE license_key IN ($placeholders) ORDER BY last_seen DESC"
    );
    $stmt->execute($keys);
    foreach ($stmt->fetchAll() as $act) {
        $activationDetails[$act['license_key']][] = $act;
    }
}

?><!DOCTYPE html>
<html lang="en">
<head>
<meta charset="UTF-8">
<title>Admin — Sidebar Buddy</title>
<style>
  *, *::before, *::after { box-sizing: border-box; margin: 0; padding: 0; }
  body { font-family: 'Segoe UI', system-ui, sans-serif; background: #0d0d10; color: #dcdcdc; font-size: 14px; }

  /* Header */
  .header { background: #13131a; border-bottom: 1px solid #2a2a3e; padding: 14px 28px; display: flex; align-items: center; justify-content: space-between; }
  .header h1 { font-size: 16px; font-weight: 700; color: #9090b0; }
  .header a { color: #555570; font-size: 12px; text-decoration: none; }
  .header a:hover { color: #aaaacc; }

  /* Stats bar */
  .stats { display: flex; gap: 16px; padding: 20px 28px; flex-wrap: wrap; }
  .stat { background: #13131a; border: 1px solid #2a2a3e; border-radius: 8px; padding: 14px 20px; min-width: 120px; }
  .stat-val { font-size: 26px; font-weight: 700; color: #e0e0e0; }
  .stat-lbl { font-size: 11px; color: #666680; margin-top: 2px; text-transform: uppercase; letter-spacing: .05em; }
  .stat.green .stat-val { color: #4ade80; }
  .stat.blue  .stat-val { color: #60a5fa; }
  .stat.purple .stat-val { color: #c084fc; }

  /* Tabs */
  .tabs { display: flex; gap: 0; padding: 0 28px; border-bottom: 1px solid #2a2a3e; }
  .tab  { padding: 10px 20px; font-size: 13px; color: #666680; cursor: pointer; border-bottom: 2px solid transparent; text-decoration: none; display: block; }
  .tab:hover { color: #aaaacc; }
  .tab.active { color: #9cdcfe; border-bottom-color: #9cdcfe; }

  /* Content */
  .content { padding: 24px 28px; }

  /* Message */
  .msg { background: #1a2e1a; border: 1px solid #3a6a3a; border-radius: 6px; padding: 10px 14px; margin-bottom: 18px; color: #80e080; font-size: 13px; }

  /* Grant comp form */
  .comp-form { background: #13131a; border: 1px solid #2a2a3e; border-radius: 8px; padding: 18px 20px; margin-bottom: 24px; display: flex; gap: 10px; flex-wrap: wrap; align-items: flex-end; }
  .comp-form label { font-size: 11px; color: #666680; text-transform: uppercase; letter-spacing:.05em; display: block; margin-bottom: 4px; }
  .comp-form input { background: #1c1c2e; border: 1px solid #2a2a3e; color: #e0e0e0; padding: 7px 10px; border-radius: 5px; font-size: 13px; width: 220px; }
  .comp-form input.wide { width: 280px; }
  .btn { background: #0e639c; color: #fff; border: none; border-radius: 5px; padding: 7px 16px; font-size: 13px; font-weight: 600; cursor: pointer; }
  .btn:hover { background: #1177bb; }
  .btn.danger { background: #6b1a1a; color: #ffaaaa; }
  .btn.danger:hover { background: #8b2a2a; }
  .btn.sm { padding: 4px 10px; font-size: 12px; }

  /* Table */
  .tbl-wrap { overflow-x: auto; }
  table { width: 100%; border-collapse: collapse; font-size: 13px; }
  th { text-align: left; color: #666680; font-size: 11px; text-transform: uppercase; letter-spacing:.05em; padding: 8px 10px; border-bottom: 1px solid #2a2a3e; white-space: nowrap; }
  td { padding: 9px 10px; border-bottom: 1px solid #1e1e2e; vertical-align: middle; }
  tr:hover td { background: #16161f; }
  .badge { display: inline-block; border-radius: 4px; padding: 2px 7px; font-size: 11px; font-weight: 600; }
  .badge.paid { background: #1a3a1a; color: #4ade80; }
  .badge.comp { background: #1a1a3a; color: #818cf8; }
  .mono { font-family: 'Courier New', monospace; font-size: 12px; color: #9cdcfe; letter-spacing:.05em; }
  .muted { color: #555570; }
</style>
</head>
<body>

<div class="header">
  <h1>&#9632; Sidebar Buddy — Admin</h1>
  <a href="?logout=1">Sign out</a>
</div>

<!-- Stats -->
<div class="stats">
  <div class="stat green">
    <div class="stat-val">$<?= number_format($revenue, 2) ?></div>
    <div class="stat-lbl">Total Revenue</div>
  </div>
  <div class="stat">
    <div class="stat-val"><?= $paidLicenses ?></div>
    <div class="stat-lbl">Paid Licenses</div>
  </div>
  <div class="stat purple">
    <div class="stat-val"><?= $compLicenses ?></div>
    <div class="stat-lbl">Comp Licenses</div>
  </div>
  <div class="stat blue">
    <div class="stat-val"><?= $totalTrials ?></div>
    <div class="stat-lbl">Trial Installs</div>
  </div>
  <div class="stat">
    <div class="stat-val"><?= $totalDownloads ?></div>
    <div class="stat-lbl">Download Clicks</div>
  </div>
  <div class="stat" style="border-color:#3a2a5e;">
    <div class="stat-val" style="color:#e879f9;"><?= $totalNotify ?></div>
    <div class="stat-lbl">Launch Signups</div>
  </div>
</div>

<!-- Tabs -->
<div class="tabs">
  <a class="tab <?= $tab === 'licenses'  ? 'active' : '' ?>" href="?tab=licenses">Licenses (<?= $totalLicenses ?>)</a>
  <a class="tab <?= $tab === 'trials'    ? 'active' : '' ?>" href="?tab=trials">Trial Installs (<?= $totalTrials ?>)</a>
  <a class="tab <?= $tab === 'downloads' ? 'active' : '' ?>" href="?tab=downloads">Downloads (<?= $totalDownloads ?>)</a>
  <a class="tab <?= $tab === 'notify'    ? 'active' : '' ?>" href="?tab=notify">Launch Signups (<?= $totalNotify ?>)</a>
</div>

<div class="content">

  <?php if ($message): ?>
    <div class="msg"><?= htmlspecialchars($message) ?></div>
  <?php endif; ?>

  <?php if ($tab === 'licenses'): ?>

    <!-- Grant comp -->
    <form method="POST" class="comp-form">
      <div>
        <label>Email</label>
        <input type="email" name="comp_email" class="wide" placeholder="recipient@example.com" required>
      </div>
      <div>
        <label>Note (optional)</label>
        <input type="text" name="comp_note" placeholder="Why this comp?">
      </div>
      <button type="submit" class="btn">Grant Comp License</button>
    </form>

    <!-- Licenses table -->
    <div class="tbl-wrap">
      <table>
        <thead>
          <tr>
            <th>Date</th>
            <th>Email</th>
            <th>Name</th>
            <th>License Key</th>
            <th>Type</th>
            <th>Amount</th>
            <th>Activations</th>
            <th></th>
          </tr>
        </thead>
        <tbody>
          <?php foreach ($licenses as $lic): ?>
          <tr>
            <td class="muted"><?= htmlspecialchars(substr($lic['created_at'] ?? '', 0, 10)) ?></td>
            <td><?= htmlspecialchars($lic['email']) ?></td>
            <td class="muted"><?= htmlspecialchars($lic['payer_name'] ?? '') ?></td>
            <td><span class="mono"><?= htmlspecialchars($lic['license_key']) ?></span></td>
            <td><span class="badge <?= $lic['type'] ?>"><?= htmlspecialchars($lic['type']) ?></span></td>
            <td><?= $lic['amount'] > 0 ? '$' . number_format((float)$lic['amount'], 2) : '—' ?></td>
            <td>
              <?php if ((int)$lic['activations'] > 0): ?>
                <button class="act-toggle" data-key="<?= htmlspecialchars($lic['license_key']) ?>"
                        style="background:none;border:none;color:#60a5fa;cursor:pointer;font-size:13px;padding:0;text-decoration:underline;">
                  <?= (int)$lic['activations'] ?> / 2 ▾
                </button>
              <?php else: ?> 0 / 2 <?php endif; ?>
            </td>
            <td>
              <form method="POST" onsubmit="return confirm('Revoke this license?')">
                <input type="hidden" name="revoke_key" value="<?= htmlspecialchars($lic['license_key']) ?>">
                <button type="submit" class="btn danger sm">Revoke</button>
              </form>
            </td>
          </tr>
          <?php if (!empty($activationDetails[$lic['license_key']])): ?>
          <tr id="act-<?= htmlspecialchars($lic['license_key']) ?>" style="display:none;background:#0c0c15;">
            <td colspan="8" style="padding:8px 10px 14px 28px;">
              <table style="width:100%;font-size:12px;border-collapse:collapse;">
                <thead><tr>
                  <th style="text-align:left;color:#444460;font-size:10px;text-transform:uppercase;letter-spacing:.05em;padding:4px 8px;border-bottom:1px solid #1e1e2e;">Device ID</th>
                  <th style="text-align:left;color:#444460;font-size:10px;text-transform:uppercase;letter-spacing:.05em;padding:4px 8px;border-bottom:1px solid #1e1e2e;">MAC Address</th>
                  <th style="text-align:left;color:#444460;font-size:10px;text-transform:uppercase;letter-spacing:.05em;padding:4px 8px;border-bottom:1px solid #1e1e2e;">Hostname</th>
                  <th style="text-align:left;color:#444460;font-size:10px;text-transform:uppercase;letter-spacing:.05em;padding:4px 8px;border-bottom:1px solid #1e1e2e;">IP</th>
                  <th style="text-align:left;color:#444460;font-size:10px;text-transform:uppercase;letter-spacing:.05em;padding:4px 8px;border-bottom:1px solid #1e1e2e;">Country</th>
                  <th style="text-align:left;color:#444460;font-size:10px;text-transform:uppercase;letter-spacing:.05em;padding:4px 8px;border-bottom:1px solid #1e1e2e;">Last Seen</th>
                </tr></thead>
                <tbody>
                  <?php foreach ($activationDetails[$lic['license_key']] as $act): ?>
                  <tr>
                    <td style="padding:5px 8px;font-family:'Courier New',monospace;font-size:11px;color:#7070a0;word-break:break-all;"><?= htmlspecialchars($act['device_id']) ?></td>
                    <td style="padding:5px 8px;" class="mono"><?= htmlspecialchars($act['mac_address'] ?? '—') ?></td>
                    <td style="padding:5px 8px;color:#9090b0;"><?= htmlspecialchars($act['hostname'] ?? '—') ?></td>
                    <td style="padding:5px 8px;" class="mono"><?= htmlspecialchars($act['ip_address'] ?? '—') ?></td>
                    <td style="padding:5px 8px;color:#9090b0;"><?= htmlspecialchars($act['country'] ?? '—') ?></td>
                    <td style="padding:5px 8px;" class="muted"><?= htmlspecialchars(substr($act['last_seen'] ?? '', 0, 16)) ?></td>
                  </tr>
                  <?php endforeach; ?>
                </tbody>
              </table>
            </td>
          </tr>
          <?php endif; ?>
          <?php endforeach; ?>
          <?php if (!$licenses): ?>
            <tr><td colspan="8" class="muted" style="padding:20px;">No licenses yet.</td></tr>
          <?php endif; ?>
        </tbody>
      </table>
    </div>

  <?php elseif ($tab === 'trials'): ?>

    <div class="tbl-wrap">
      <table>
        <thead>
          <tr>
            <th>First Seen</th>
            <th>Last Seen</th>
            <th>Launches</th>
            <th>IP</th>
            <th>Location</th>
            <th>MAC Address</th>
            <th>Device ID</th>
          </tr>
        </thead>
        <tbody>
          <?php foreach ($trials as $t): ?>
          <tr>
            <td class="muted"><?= htmlspecialchars(substr($t['first_seen'] ?? '', 0, 10)) ?></td>
            <td class="muted"><?= htmlspecialchars(substr($t['last_seen']  ?? '', 0, 10)) ?></td>
            <td><?= (int)$t['launch_count'] ?></td>
            <td class="mono"><?= htmlspecialchars($t['ip_address'] ?? '—') ?></td>
            <td><?= htmlspecialchars(implode(', ', array_filter([$t['city'] ?? null, $t['country'] ?? null])) ?: '—') ?></td>
            <td class="mono"><?= htmlspecialchars($t['mac_address'] ?? '—') ?></td>
            <td style="font-family:'Courier New',monospace;font-size:11px;color:#7070a0;word-break:break-all;max-width:200px;"><?= htmlspecialchars($t['device_id'] ?? '—') ?></td>
          </tr>
          <?php endforeach; ?>
          <?php if (!$trials): ?>
            <tr><td colspan="6" class="muted" style="padding:20px;">No trial installs yet.</td></tr>
          <?php endif; ?>
        </tbody>
      </table>
    </div>

  <?php elseif ($tab === 'downloads'): ?>

    <div class="tbl-wrap">
      <table>
        <thead>
          <tr>
            <th>Date / Time</th>
            <th>IP</th>
            <th>Location</th>
            <th>User Agent</th>
          </tr>
        </thead>
        <tbody>
          <?php foreach ($downloads as $dl): ?>
          <tr>
            <td class="muted"><?= htmlspecialchars($dl['clicked_at'] ?? '') ?></td>
            <td class="mono"><?= htmlspecialchars($dl['ip_address'] ?? '—') ?></td>
            <td><?= htmlspecialchars(implode(', ', array_filter([$dl['city'] ?? null, $dl['region'] ?? null, $dl['country'] ?? null])) ?: '—') ?></td>
            <td class="muted" style="font-size:11px;"><?= htmlspecialchars(substr($dl['user_agent'] ?? '', 0, 80)) ?></td>
          </tr>
          <?php endforeach; ?>
          <?php if (!$downloads): ?>
            <tr><td colspan="4" class="muted" style="padding:20px;">No download clicks yet.</td></tr>
          <?php endif; ?>
        </tbody>
      </table>
    </div>

  <?php elseif ($tab === 'notify'): ?>

    <div style="display:flex;align-items:center;justify-content:space-between;margin-bottom:16px;">
      <p style="color:#666680;font-size:13px;"><?= $totalNotify ?> email<?= $totalNotify !== 1 ? 's' : '' ?> waiting for launch.</p>
      <?php if ($totalNotify > 0): ?>
        <a href="?export=notify" class="btn" style="text-decoration:none;font-size:12px;padding:5px 14px;">Export CSV</a>
      <?php endif; ?>
    </div>

    <div class="tbl-wrap">
      <table>
        <thead>
          <tr>
            <th>Email</th>
            <th>Signed Up</th>
          </tr>
        </thead>
        <tbody>
          <?php foreach ($notifyList as $n): ?>
          <tr>
            <td><?= htmlspecialchars($n['email']) ?></td>
            <td class="muted"><?= htmlspecialchars($n['created_at']) ?></td>
          </tr>
          <?php endforeach; ?>
          <?php if (!$notifyList): ?>
            <tr><td colspan="2" class="muted" style="padding:20px;">No signups yet.</td></tr>
          <?php endif; ?>
        </tbody>
      </table>
    </div>

  <?php endif; ?>

</div>
<script>
  document.querySelectorAll('.act-toggle').forEach(function(btn) {
    btn.addEventListener('click', function() {
      var row = document.getElementById('act-' + btn.dataset.key);
      if (row) row.style.display = row.style.display === 'none' ? '' : 'none';
    });
  });
</script>
</body>
</html>
