<?php
/**
 * Sidebar Buddy — Student License Request Handler
 * Requires a .edu email; notifies support for manual review and comp issuance.
 */

require_once __DIR__ . '/private/secrets.php';
require_once __DIR__ . '/private/resend_mailer.php';
define('RATE_LIMIT_DIR', sys_get_temp_dir() . '/sb_student_rl/');
define('RATE_LIMIT_SECS', 3600);

header('Content-Type: application/json');

if ($_SERVER['REQUEST_METHOD'] !== 'POST') {
    http_response_code(405);
    echo json_encode(['success' => false]);
    exit;
}

$input = json_decode(file_get_contents('php://input'), true);

// Honeypot — silently succeed to fool bots
if (!empty($input['website'])) {
    echo json_encode(['success' => true]);
    exit;
}

$name  = trim($input['name']  ?? '');
$email = strtolower(filter_var(trim($input['email'] ?? ''), FILTER_SANITIZE_EMAIL));

if (!$name || !filter_var($email, FILTER_VALIDATE_EMAIL)) {
    http_response_code(400);
    echo json_encode(['success' => false, 'error' => 'Please fill in all fields with a valid email address.']);
    exit;
}

if (substr($email, -4) !== '.edu') {
    http_response_code(400);
    echo json_encode(['success' => false, 'error' => 'Please enter a valid .edu email address.']);
    exit;
}

// Rate limit by IP
$ip  = $_SERVER['REMOTE_ADDR'] ?? '0.0.0.0';
$key = RATE_LIMIT_DIR . md5($ip) . '.txt';
$now = time();

if (!is_dir(RATE_LIMIT_DIR)) {
    @mkdir(RATE_LIMIT_DIR, 0700, true);
}

if (file_exists($key)) {
    $last = (int) file_get_contents($key);
    if (($now - $last) < RATE_LIMIT_SECS) {
        http_response_code(429);
        echo json_encode(['success' => false, 'error' => 'A request was already submitted from this connection. Please wait before trying again.']);
        exit;
    }
}

file_put_contents($key, $now);

$subject = '[Student License] ' . $name . ' <' . $email . '>';
$body    = "Student License Request\n"
         . "=======================\n\n"
         . "Name:      {$name}\n"
         . "Email:     {$email}\n"
         . "Submitted: " . date('Y-m-d H:i:s') . " UTC\n"
         . "IP:        {$ip}\n\n"
         . "Issue a comp license via the admin panel and reply to the student's .edu address.";

resendMailText(NOTIFY_EMAIL, $subject, $body, $email);

echo json_encode(['success' => true]);
