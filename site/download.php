<?php
/**
 * Sidebar Buddy — Download Tracker
 *
 * Sends an admin notification email then redirects to the
 * installer on GitHub Releases. Bot/crawler downloads are
 * logged but do not trigger emails.
 */

require_once __DIR__ . '/private/secrets.php';
require_once __DIR__ . '/private/resend_mailer.php';

define('WIN_URL', 'https://github.com/dante-rinaldi/sidebarbuddy-releases/releases/latest/download/SidebarBuddy-Setup.exe');

$downloadUrl   = WIN_URL;
$platformLabel = 'Windows';

// Collect context
$rawIp     = $_SERVER['HTTP_X_FORWARDED_FOR'] ?? $_SERVER['REMOTE_ADDR'] ?? 'unknown';
$ip        = trim(explode(',', $rawIp)[0]);
$userAgent = $_SERVER['HTTP_USER_AGENT'] ?? 'unknown';
$referer   = htmlspecialchars($_SERVER['HTTP_REFERER'] ?? 'direct', ENT_QUOTES, 'UTF-8');
$timestamp = date('Y-m-d H:i:s') . ' UTC';

// Rate limit: max 5 notification emails per IP per minute
$rlKey  = sys_get_temp_dir() . '/sb_dl_rl_' . md5($ip) . '.json';
$now    = time();
$rlData = file_exists($rlKey) ? (json_decode(file_get_contents($rlKey), true) ?: []) : [];
$rlData = array_values(array_filter($rlData, fn($t) => $now - $t < 60));
$tooMany = count($rlData) >= 5;
$rlData[] = $now;
file_put_contents($rlKey, json_encode($rlData), LOCK_EX);

// Bot / crawler detection
$botPatterns = [
    'googlebot','bingbot','slurp','duckduckbot','baiduspider','yandexbot',
    'sogou','exabot','facebot','ia_archiver','semrushbot','ahrefsbot',
    'mj12bot','dotbot','petalbot','crawler','spider','bot/','wget/','curl/',
    'python-requests','go-http-client','java/','libwww','scrapy','headlesschrome',
];
$uaLower    = strtolower($userAgent);
$isBot      = false;
$botMatched = '';
foreach ($botPatterns as $pat) {
    if (str_contains($uaLower, $pat)) { $isBot = true; $botMatched = $pat; break; }
}

// Geolocation via ip-api.com (free, no key)
$geoLabel = 'unavailable';
if ($ip !== 'unknown' && filter_var($ip, FILTER_VALIDATE_IP, FILTER_FLAG_NO_PRIV_RANGE | FILTER_FLAG_NO_RES_RANGE)) {
    $raw = @file_get_contents("http://ip-api.com/json/" . urlencode($ip) . "?fields=status,country,regionName,city",
        false, stream_context_create(['http' => ['timeout' => 3]]));
    if ($raw) {
        $geo = json_decode($raw, true);
        if (($geo['status'] ?? '') === 'success')
            $geoLabel = implode(', ', array_filter([$geo['city'] ?? '', $geo['regionName'] ?? '', $geo['country'] ?? '']));
    }
}

// Send admin notification
$mailSent = false;
if (!$tooMany && !$isBot) {
    $subject = '[Sidebar Buddy] New Download';
    $body    = "Someone downloaded Sidebar Buddy!\n\n"
             . "Platform:   {$platformLabel}\n"
             . "Time:       {$timestamp}\n"
             . "IP:         {$ip}\n"
             . "Location:   {$geoLabel}\n"
             . "Referrer:   {$referer}\n"
             . "User Agent: {$userAgent}\n";
    $mailSent = (bool)resendMailText(NOTIFY_EMAIL, $subject, $body);
}

// Log everything
$logLine = "[{$timestamp}] platform={$platformLabel} ip={$ip} location=\"{$geoLabel}\" bot=" . ($isBot ? "yes({$botMatched})" : 'no') . " mail=" . ($mailSent ? 'yes' : 'no') . "\n";
@file_put_contents(__DIR__ . '/logs/downloads.log', $logLine, FILE_APPEND | LOCK_EX);

// Redirect to installer
header('Location: ' . $downloadUrl, true, 302);
exit;
