<?php
/**
 * Sidebar Buddy - Launch Notification Signup
 * Saves email to notify_list table and alerts the owner.
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
$email = isset($input['email']) ? trim($input['email']) : '';

if (!filter_var($email, FILTER_VALIDATE_EMAIL)) {
    http_response_code(400);
    echo json_encode(['success' => false, 'error' => 'Please enter a valid email address.']);
    exit;
}

try {
    $pdo = new PDO(
        'mysql:host=' . DB_HOST . ';dbname=' . DB_NAME . ';charset=utf8mb4',
        DB_USER, DB_PASS,
        [PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION]
    );

    $pdo->exec("CREATE TABLE IF NOT EXISTS notify_list (
        id         INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
        email      VARCHAR(255) NOT NULL UNIQUE,
        created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4");

    $pdo->prepare('INSERT IGNORE INTO notify_list (email) VALUES (?)')->execute([$email]);

} catch (PDOException $e) {
    http_response_code(500);
    echo json_encode(['success' => false, 'error' => 'Could not save your email. Please try again.']);
    exit;
}

resendMailText(
    NOTIFY_EMAIL,
    '[Sidebar Buddy] Launch notification signup',
    "Someone signed up to be notified at launch.\n\nEmail: {$email}\nTime:  " . date('Y-m-d H:i:s T')
);

echo json_encode(['success' => true]);
