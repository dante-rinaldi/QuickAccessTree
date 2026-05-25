<?php
$pageTitle = 'Changelog - Sidebar Buddy';
?>
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1.0" />
  <title><?= htmlspecialchars($pageTitle) ?></title>
  <meta name="description" content="Sidebar Buddy release history — see what's new, what's fixed, and what's changed in every version." />
  <link rel="canonical" href="https://sidebarbuddy.com/changelog" />
  <link rel="icon" type="image/x-icon" href="/logo/logo%20SideBar%20Buddy%20Icon.ico" />
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
    .policy-page a { color: var(--accent); text-decoration: underline; }
    .back-link {
      display: inline-flex;
      align-items: center;
      gap: 6px;
      font-size: 0.85rem;
      color: #d4d4e8;
      margin-bottom: 36px;
      text-decoration: none;
    }
    .back-link:hover { color: var(--text); }

    .release {
      margin-bottom: 40px;
      border-left: 2px solid #2a2a3e;
      padding-left: 20px;
    }
    .release-header {
      display: flex;
      align-items: baseline;
      gap: 12px;
      margin-bottom: 14px;
    }
    .release-version {
      color: #9CDCFE;
      font-size: 1.05rem;
      font-weight: 700;
      font-family: 'Consolas', 'Courier New', monospace;
    }
    .release-date {
      color: #666;
      font-size: 0.82rem;
    }
    .release ul {
      padding-left: 1.2em;
      margin: 0;
    }
    .release li {
      color: #c0c0d0;
      font-size: 0.93rem;
      line-height: 1.8;
      margin-bottom: 2px;
    }
    .release li span.tag {
      font-size: 0.72rem;
      font-weight: 700;
      text-transform: uppercase;
      letter-spacing: 0.05em;
      border-radius: 3px;
      padding: 1px 5px;
      margin-right: 6px;
      vertical-align: middle;
    }
    .tag-fixed  { background: #3a2222; color: #f87171; }
    .tag-added  { background: #1a3a22; color: #6ee7a0; }
    .tag-changed { background: #2a2a1a; color: #fbbf24; }
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
    <h1>Changelog</h1>
    <p class="meta">Full release history for Sidebar Buddy.</p>

    <div class="release">
      <div class="release-header">
        <span class="release-version">v1.0.2</span>
        <span class="release-date">May 2026</span>
      </div>
      <ul>
        <li><span class="tag tag-fixed">Fixed</span>Sidebar became unclickable after closing and reopening Explorer</li>
        <li><span class="tag tag-fixed">Fixed</span>Color picker in "Per Folder Only" mode was cascading to children</li>
        <li><span class="tag tag-fixed">Fixed</span>"Reset Color" was wiping children's colors</li>
        <li><span class="tag tag-fixed">Fixed</span>Setting or resetting a color collapsed the entire tree</li>
        <li><span class="tag tag-fixed">Fixed</span>Visibility delay setting was ignored until Settings was opened</li>
        <li><span class="tag tag-fixed">Fixed</span>Installer error when upgrading over a running instance</li>
        <li><span class="tag tag-added">Added</span>"Check for Updates" button and version display on License page</li>
        <li><span class="tag tag-changed">Changed</span>Show This PC and Control Panel now enabled by default</li>
        <li><span class="tag tag-changed">Changed</span>Clearer labels for Auto-hide and Restore Expanded State settings</li>
      </ul>
    </div>

    <div class="release">
      <div class="release-header">
        <span class="release-version">v1.0.1</span>
        <span class="release-date">May 2026</span>
      </div>
      <ul>
        <li><span class="tag tag-fixed">Fixed</span>Folder links stop working after closing and reopening Explorer</li>
        <li><span class="tag tag-fixed">Fixed</span>Highlight color picker caused sidebar tree to jump to first folder</li>
        <li><span class="tag tag-added">Added</span>Branded HTML email templates for comp licenses and verification</li>
        <li><span class="tag tag-changed">Changed</span>Updated app icon to match latest logo design</li>
      </ul>
    </div>

    <div class="release">
      <div class="release-header">
        <span class="release-version">v1.0.0</span>
        <span class="release-date">May 2026</span>
      </div>
      <ul>
        <li><span class="tag tag-added">Added</span>Initial release</li>
      </ul>
    </div>

  </div>

  <footer>
    <div class="footer-inner">
      <div class="footer-links">
        <a href="/terms">Terms of Service</a>
        <a href="/privacy">Privacy Policy</a>
        <a href="/contact">Support</a>
        <a href="/changelog">Changelog</a>
      </div>
      <p class="footer-copy">&copy; 2026 Sidebar Buddy. All rights reserved.</p>
    </div>
  </footer>

</body>
</html>
