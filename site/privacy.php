<?php
/**
 * Sidebar Buddy — Privacy Policy
 */
$pageTitle = 'Privacy Policy - Sidebar Buddy';
?>
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1.0" />
  <title><?= htmlspecialchars($pageTitle) ?></title>
  <link rel="stylesheet" href="style.css" />
  <style>
    .policy-page {
      max-width: 760px;
      margin: 0 auto;
      padding: 56px 24px 80px;
      position: relative;
      z-index: 1;
    }
    .policy-page h1 {
      font-size: 2rem;
      margin-bottom: 8px;
      color: var(--text);
    }
    .policy-page .meta {
      font-size: 0.85rem;
      color: #d4d4e8;
      margin-bottom: 48px;
    }
    .policy-page h2 {
      font-size: 1.1rem;
      color: var(--accent);
      margin: 36px 0 10px;
      text-transform: uppercase;
      letter-spacing: 0.06em;
      font-size: 0.8rem;
    }
    .policy-page p, .policy-page li {
      color: #c0c0d0;
      line-height: 1.75;
      font-size: 0.95rem;
    }
    .policy-page ul {
      padding-left: 1.4em;
      margin-top: 8px;
    }
    .policy-page li { margin-bottom: 6px; }
    .policy-page a { color: var(--accent); text-decoration: underline; }
    .back-link {
      display: inline-flex;
      align-items: center;
      gap: 6px;
      font-size: 0.85rem;
      color: #d4d4e8;
      margin-bottom: 36px;
    }
    .back-link:hover { color: var(--text); }
  </style>
</head>
<body>

  <!-- NAV -->
  <nav>
    <div class="nav-inner">
      <a href="/">
        <img src="logo/logo_sideBarBuddy_800px.webp" alt="Sidebar Buddy" style="height:38px;width:auto;border-radius:4px;">
      </a>
    </div>
  </nav>

  <div class="policy-page">
    <div style="text-align:center;margin-bottom:40px;">
      <img src="logo/logo_sideBarBuddy_800px.webp" alt="Sidebar Buddy" style="height:96px;width:auto;border-radius:10px;">
    </div>
    <a href="/" class="back-link">&#8592; Back to home</a>
    <h1>Privacy Policy</h1>
    <p class="meta">Last updated: May 2026</p>

    <p>Your privacy matters to us. This policy explains what information Sidebar Buddy ("the App") and this website collect, how it is used, and how it is protected.</p>

    <h2>1. The App — Local Data Only</h2>
    <p>Sidebar Buddy runs entirely on your local device. The App:</p>
    <ul>
      <li>Does not collect, transmit, or upload any personal data</li>
      <li>Does not have access to your files beyond the folder paths you choose to pin</li>
      <li>Stores all your settings (shortcuts, groups, color preferences, skin) locally in your user profile folder</li>
      <li>Does not require an internet connection after license activation</li>
      <li>Does not include analytics, telemetry, or crash reporting that sends data off your device</li>
    </ul>

    <h2>2. License Activation</h2>
    <p>When you enter a license key, the App validates it locally against your stored purchase record. No request is made to an external server during this process. Your license key is stored locally and is not transmitted back to us.</p>

    <h2>3. Website — Information We Collect</h2>
    <p>When you visit <strong>sidebarbuddy.com</strong> or interact with our purchase or notification forms, we may collect:</p>
    <ul>
      <li><strong>Email address</strong> - when you sign up for launch notifications or complete a purchase. Used to deliver your license key and important account notices.</li>
      <li><strong>Payment information</strong> - processed entirely by PayPal. We never see or store your credit card number.</li>
      <li><strong>IP address and basic server logs</strong> - retained temporarily by our hosting provider for security and diagnostics.</li>
    </ul>

    <h2>4. How We Use Your Information</h2>
    <ul>
      <li>To deliver your license key by email after purchase</li>
      <li>To notify you when the App launches (launch notification list only)</li>
      <li>To respond to support requests</li>
      <li>To detect and prevent fraud or abuse</li>
    </ul>
    <p>We do not sell, rent, or share your personal information with third parties for marketing purposes.</p>

    <h2>5. Third-Party Services</h2>
    <ul>
      <li><strong>PayPal</strong> - handles all payment processing. Their privacy policy applies to payment data.</li>
      <li><strong>Resend</strong> - used to send transactional emails (license delivery, notifications). Resend may process your email address to deliver these messages.</li>
    </ul>

    <h2>6. Cookies</h2>
    <p>This website does not use tracking or advertising cookies. Basic session functionality may set a short-lived cookie, but no persistent tracking cookies are used.</p>

    <h2>7. Data Retention</h2>
    <p>Purchase records (email + license key) are retained as long as necessary to support your license. Notification-only signups are retained until the launch notification is sent, after which they are deleted. You may request deletion of your data at any time by emailing us.</p>

    <h2>8. Children's Privacy</h2>
    <p>Sidebar Buddy is not directed at children under 13. We do not knowingly collect personal information from children under 13. If you believe a child has provided us with their information, please contact us and we will delete it promptly.</p>

    <h2>9. Security</h2>
    <p>We use industry-standard practices to protect your data, including encrypted connections (HTTPS) and restricted server access. No method of transmission over the internet is 100% secure, but we take reasonable precautions.</p>

    <h2>10. Changes to This Policy</h2>
    <p>We may update this policy from time to time. The "Last updated" date at the top reflects the most recent revision. Continued use of the App or website after changes are posted constitutes acceptance.</p>

    <h2>11. Contact</h2>
    <p>Questions or data requests? <a href="/contact">Contact us here</a>.</p>
  </div>

  <footer>
    <div class="footer-inner">
      <div class="footer-links">
        <a href="/terms">Terms of Service</a>
        <a href="/privacy">Privacy Policy</a>
        <a href="/contact">Support</a>
      </div>
      <p class="footer-copy">&copy; 2026 Sidebar Buddy. All rights reserved.</p>
    </div>
  </footer>

</body>
</html>
