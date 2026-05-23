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
            $body    = "Hi,\n\nHere is your complimentary Sidebar Buddy license.\n\n"
                     . "Key: {$compKey}\n\n"
                     . "To activate: open Sidebar Buddy → Settings → License and enter your email and key.\n\n"
                     . "— Sidebar Buddy";
            resendMailText($compEmail, $subject, $body);

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
            <td><?= (int)$lic['activations'] ?> / 2</td>
            <td>
              <form method="POST" onsubmit="return confirm('Revoke this license?')">
                <input type="hidden" name="revoke_key" value="<?= htmlspecialchars($lic['license_key']) ?>">
                <button type="submit" class="btn danger sm">Revoke</button>
              </form>
            </td>
          </tr>
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
            <td class="muted" style="font-size:11px;"><?= htmlspecialchars(substr($t['device_id'] ?? '', 0, 16)) ?>…</td>
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
</body>
</html>
