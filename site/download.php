<?php
/**
 * Sidebar Buddy — Download Tracker
 *
 * Logs the click with IP + geolocation, emails the owner, then redirects
 * to the actual .exe download.
 */

require_once __DIR__ . '/private/secrets.php';
require_once __DIR__ . '/private/resend_mailer.php';

// Resolve IP
$rawIp = $_SERVER['HTTP_X_FORWARDED_FOR'] ?? $_SERVER['REMOTE_ADDR'] ?? 'unknown';
$ip    = trim(explode(',', $rawIp)[0]);
$ua    = substr($_SERVER['HTTP_USER_AGENT'] ?? '', 0, 500);

// Geolocation (best-effort)
$country = $city = null;
if ($ip && $ip !== '127.0.0.1' && $ip !== '::1') {
    $geo = @file_get_contents("http://ip-api.com/json/{$ip}?fields=country,city,regionName");
    if ($geo) {
        $gd      = json_decode($geo, true);
        $country = $gd['country']    ?? null;
        $city    = $gd['city']       ?? null;
        $region  = $gd['regionName'] ?? null;
    }
}

// Log to DB
try {
    $pdo = new PDO(
        'mysql:host=' . DB_HOST . ';dbname=' . DB_NAME . ';charset=utf8mb4',
        DB_USER, DB_PASS,
        [PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION]
    );

    $pdo->exec("
        CREATE TABLE IF NOT EXISTS downloads (
            id         INT AUTO_INCREMENT PRIMARY KEY,
            ip_address VARCHAR(45),
            country    VARCHAR(100),
            city       VARCHAR(100),
            region     VARCHAR(100),
            user_agent VARCHAR(500),
            clicked_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
    ");

    $pdo->prepare(
        'INSERT INTO downloads (ip_address, country, city, region, user_agent) VALUES (?, ?, ?, ?, ?)'
    )->execute([$ip, $country, $city, $region ?? null, $ua]);

} catch (Exception $e) {
    error_log('SB download log error: ' . $e->getMessage());
}

// Email owner
$loc     = implode(', ', array_filter([$city, $region ?? null, $country]));
$subject = '[Sidebar Buddy] Download clicked';
$body    = "Someone downloaded Sidebar Buddy.\n\n"
         . "IP:       {$ip}\n"
         . "Location: " . ($loc ?: 'Unknown') . "\n"
         . "Time:     " . date('Y-m-d H:i:s T') . "\n"
         . "Agent:    {$ua}\n";
resendMailText(NOTIFY_EMAIL, $subject, $body);

// Redirect to actual file
header('Location: ' . DOWNLOAD_URL);
exit;
