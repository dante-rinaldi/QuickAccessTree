<?php
/**
 * Sidebar Buddy — Terms of Service
 */
$pageTitle = 'Terms of Service - Sidebar Buddy';
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
    <h1>Terms of Service</h1>
    <p class="meta">Last updated: May 2026</p>

    <p>By downloading, installing, or using Sidebar Buddy ("the App"), you agree to these Terms of Service. If you do not agree, do not use the App.</p>

    <h2>1. License</h2>
    <p>Sidebar Buddy is sold as a one-time purchase. Purchasing the App grants you a personal, non-transferable, non-exclusive license to install and use one copy of Sidebar Buddy on your own Windows device(s). You may not sell, sublicense, distribute, or share your license key with others.</p>

    <h2>2. Free Trial</h2>
    <p>A 15-day free trial is available without a license key. At the end of the trial period, certain features will be restricted until a valid license key is entered. Trial use is subject to these same terms.</p>

    <h2>3. One-Time Purchase &amp; Refunds</h2>
    <p>The App is sold for a one-time fee of $10.00 USD. Because digital software is immediately usable upon delivery, all sales are final. If you experience a technical issue, please contact support before requesting a refund — we will make every reasonable effort to resolve it.</p>

    <h2>4. Permitted Use</h2>
    <p>You may use the App for personal or professional purposes on your own devices. You may not:</p>
    <ul>
      <li>Reverse-engineer, decompile, or disassemble the App</li>
      <li>Modify, create derivative works from, or tamper with the App</li>
      <li>Use the App for any unlawful purpose</li>
      <li>Remove or alter any proprietary notices</li>
    </ul>

    <h2>5. What the App Does and Does Not Do</h2>
    <p>Sidebar Buddy is a local desktop utility that displays a customizable shortcut sidebar alongside Windows Explorer. The App:</p>
    <ul>
      <li>Reads only the folder paths and settings you configure</li>
      <li>Does not scan, index, upload, or transmit your files or personal data</li>
      <li>Does not require an internet connection to function after license activation</li>
      <li>Stores all settings locally on your device</li>
    </ul>

    <h2>6. License Activation</h2>
    <p>License keys are delivered by email after purchase. Each key is tied to your purchase and may be used on your personal device(s). We reserve the right to deactivate keys used in violation of these terms (e.g., shared or resold keys).</p>

    <h2>7. Updates</h2>
    <p>Updates may be provided at our discretion. We are not obligated to provide updates, new features, or continued support for any particular version.</p>

    <h2>8. Disclaimer of Warranties</h2>
    <p>The App is provided "as is" without warranties of any kind, express or implied, including but not limited to fitness for a particular purpose or non-infringement. We do not warrant that the App will be error-free or uninterrupted.</p>

    <h2>9. Limitation of Liability</h2>
    <p>To the maximum extent permitted by law, Inferno Creative Studio shall not be liable for any indirect, incidental, special, or consequential damages arising from your use of the App, even if advised of the possibility of such damages. Our total liability shall not exceed the amount you paid for the App.</p>

    <h2>10. Termination</h2>
    <p>Your license terminates automatically if you violate these terms. Upon termination you must cease using and delete all copies of the App.</p>

    <h2>11. Governing Law</h2>
    <p>These terms are governed by the laws of the State of Florida, United States, without regard to conflict of law principles.</p>

    <h2>12. Changes to These Terms</h2>
    <p>We may update these terms from time to time. Continued use of the App after changes are posted constitutes acceptance of the updated terms. The "Last updated" date at the top of this page reflects the most recent revision.</p>

    <h2>13. Contact</h2>
    <p>Questions about these terms? Email us at <a href="mailto:support@sidebarbuddy.com">support@sidebarbuddy.com</a>.</p>
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
