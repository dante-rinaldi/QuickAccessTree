<?php
/**
 * Sidebar Buddy — Contact Form Handler
 */

require_once __DIR__ . '/private/secrets.php';
require_once __DIR__ . '/private/resend_mailer.php';
define('RATE_LIMIT_DIR', sys_get_temp_dir() . '/sb_contact_rl/');
define('RATE_LIMIT_SECS', 300);

header('Content-Type: application/json');

if ($_SERVER['REQUEST_METHOD'] !== 'POST') {
    http_response_code(405);
    echo json_encode(['success' => false]);
    exit;
}

$input = json_decode(file_get_contents('php://input'), true);

// Honeypot
if (!empty($input['website'])) {
    echo json_encode(['success' => true]);
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
        echo json_encode(['success' => false, 'error' => 'Please wait a few minutes before sending another message.']);
        exit;
    }
}

// Validate
$name    = trim($input['name']    ?? '');
$email   = filter_var(trim($input['email'] ?? ''), FILTER_SANITIZE_EMAIL);
$subject = trim($input['subject'] ?? '');
$message = trim($input['message'] ?? '');

$allowed_subjects = [
    'I need help with the app',
    'License or activation issue',
    'Refund request',
    'Suggest a feature',
    'Report a bug',
    'Other',
];

if (!$name || !$message || !filter_var($email, FILTER_VALIDATE_EMAIL)) {
    http_response_code(400);
    echo json_encode(['success' => false, 'error' => 'Please fill in all fields with a valid email address.']);
    exit;
}

if (!in_array($subject, $allowed_subjects, true)) {
    http_response_code(400);
    echo json_encode(['success' => false, 'error' => 'Please select a valid topic.']);
    exit;
}

file_put_contents($key, $now);

$email_subject = '[Support] ' . $subject . ' — ' . $name;
$body = "Support Request\n"
      . "===============\n\n"
      . "Name:      {$name}\n"
      . "Email:     {$email}\n"
      . "Topic:     {$subject}\n"
      . "Submitted: " . date('Y-m-d H:i:s') . " UTC\n"
      . "IP:        {$ip}\n\n"
      . "Message:\n{$message}";

resendMailText(NOTIFY_EMAIL, $email_subject, $body, $email);

echo json_encode(['success' => true]);
