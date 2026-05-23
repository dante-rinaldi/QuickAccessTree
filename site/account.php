<?php
/**
 * Sidebar Buddy — My Account page
 */
?>
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1.0" />
  <title>My Account - Sidebar Buddy</title>
  <meta name="robots" content="noindex, follow" />
  <link rel="stylesheet" href="style.css" />
  <style>
    .account-page {
      max-width: 560px;
      margin: 0 auto;
      padding: 72px 24px 80px;
      position: relative;
      z-index: 1;
      text-align: center;
    }
    .account-page img.acct-logo {
      height: 52px;
      width: auto;
      margin-bottom: 36px;
    }
    .account-page h1 {
      font-size: 1.75rem;
      font-weight: 700;
      color: var(--text);
      margin-bottom: 12px;
    }
    .account-page .subtitle {
      color: #9090b8;
      font-size: 1rem;
      line-height: 1.7;
      margin-bottom: 40px;
    }
    .info-card {
      background: #13131f;
      border: 1px solid #2a2a3e;
      border-radius: 12px;
      padding: 28px 32px;
      text-align: left;
      margin-bottom: 28px;
    }
    .info-card h2 {
      font-size: 0.7rem;
      font-weight: 600;
      text-transform: uppercase;
      letter-spacing: 0.1em;
      color: #5050a0;
      margin: 0 0 16px;
    }
    .info-card ul {
      list-style: none;
      padding: 0;
      margin: 0;
    }
    .info-card ul li {
      color: #b0b0d0;
      font-size: 0.95rem;
      line-height: 1.7;
      padding: 6px 0;
      border-bottom: 1px solid #1e1e30;
      display: flex;
      align-items: center;
      gap: 10px;
    }
    .info-card ul li:last-child { border-bottom: none; }
    .info-card ul li span.dot {
      color: #40c4ff;
      font-size: 1.1rem;
      flex-shrink: 0;
    }
    .acct-actions {
      display: flex;
      gap: 12px;
      justify-content: center;
      flex-wrap: wrap;
    }
    .acct-actions a {
      display: inline-block;
      padding: 13px 28px;
      border-radius: 8px;
      font-size: 0.95rem;
      font-weight: 600;
      text-decoration: none;
      cursor: pointer;
    }
    .btn-primary-acct {
      background: #0e639c;
      color: #fff;
    }
    .btn-secondary-acct {
      background: transparent;
      color: #9090b8;
      border: 1px solid #2a2a3e;
    }
    .btn-secondary-acct:hover { color: var(--text); border-color: #4a4a6e; }
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
        <a href="/contact">Contact</a>
      </div>
    </div>
  </nav>

  <div class="account-page">
    <img src="logo/logo_sideBarBuddy_800px.webp" alt="Sidebar Buddy" class="acct-logo">
    <h1>My Account</h1>
    <p class="subtitle">
      Sidebar Buddy stores your license locally — no login needed.<br>
      Your key is saved in the app under <strong style="color:#d0d0e8;">Settings &rsaquo; License</strong>.
    </p>

    <div class="info-card">
      <h2>What you can do here</h2>
      <ul>
        <li><span class="dot">&#10003;</span> Lost your key? Email us and we'll resend it.</li>
        <li><span class="dot">&#10003;</span> Need to move to a new PC? We'll reset your activation.</li>
        <li><span class="dot">&#10003;</span> Have a billing question? We're happy to help.</li>
      </ul>
    </div>

    <div class="acct-actions">
      <a href="/contact" class="btn-primary-acct">Contact Support</a>
      <a href="/" class="btn-secondary-acct">Back to Home</a>
    </div>
  </div>

</body>
</html>
