<?php
/**
 * Sidebar Buddy — Contact / Support page
 */
?>
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1.0" />
  <title>Contact Support - Sidebar Buddy</title>
  <meta name="robots" content="noindex, follow" />
  <link rel="stylesheet" href="style.css" />
  <style>
    .contact-page {
      max-width: 640px;
      margin: 0 auto;
      padding: 56px 24px 80px;
      position: relative;
      z-index: 1;
    }
    .contact-page h1 {
      font-size: 2rem;
      font-weight: 700;
      margin-bottom: 8px;
      color: var(--text);
    }
    .contact-page .subtitle {
      color: #d4d4e8;
      margin-bottom: 48px;
      font-size: 0.95rem;
    }
    .contact-card {
      background: var(--bg2);
      border: 1px solid var(--border);
      border-radius: var(--radius);
      padding: 36px 40px;
    }
    .contact-card label {
      display: block;
      font-size: 0.85rem;
      color: #d4d4e8;
      margin-bottom: 6px;
    }
    .contact-card input[type="text"],
    .contact-card input[type="email"],
    .contact-card select,
    .contact-card textarea {
      width: 100%;
      background: var(--bg3);
      border: 1px solid var(--border);
      border-radius: 6px;
      color: var(--text);
      font-family: var(--font);
      font-size: 0.9rem;
      padding: 10px 14px;
      outline: none;
      transition: border-color 0.15s;
    }
    .contact-card input:focus,
    .contact-card select:focus,
    .contact-card textarea:focus {
      border-color: var(--accent);
    }
    .contact-card select {
      appearance: none;
      -webkit-appearance: none;
      background-image: url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='12' height='8' viewBox='0 0 12 8'%3E%3Cpath fill='%23888899' d='M1 1l5 5 5-5'/%3E%3C/svg%3E");
      background-repeat: no-repeat;
      background-position: right 14px center;
      cursor: pointer;
    }
    .contact-card select option { background: var(--bg3); }
    .contact-card textarea { resize: vertical; min-height: 140px; }
    .form-honeypot { display: none !important; }
    .form-row {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 16px;
    }
    @media (max-width: 500px) { .form-row { grid-template-columns: 1fr; } }
    .form-group { margin-bottom: 20px; }
    .form-submit-row {
      display: flex;
      align-items: center;
      gap: 16px;
      flex-wrap: wrap;
      margin-top: 4px;
    }
    .btn-contact {
      background: var(--accent);
      color: #fff;
      border: none;
      border-radius: 8px;
      padding: 10px 24px;
      font-size: 0.9rem;
      font-weight: 600;
      cursor: pointer;
      transition: background 0.15s, opacity 0.15s;
    }
    .btn-contact:hover { background: #3a8eef; }
    .btn-contact:disabled { opacity: 0.5; cursor: not-allowed; }
    .form-note {
      font-size: 0.8rem;
      color: #d4d4e8;
    }
    .form-feedback {
      margin-top: 16px;
      font-size: 0.88rem;
      padding: 10px 14px;
      border-radius: 6px;
      display: none;
    }
    .form-feedback.success { display: block; background: rgba(74,222,128,0.1); color: #4ade80; border: 1px solid rgba(74,222,128,0.3); }
    .form-feedback.error   { display: block; background: rgba(255,107,107,0.1); color: #ff6b6b; border: 1px solid rgba(255,107,107,0.3); }
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
      <div class="nav-links">
        <a href="/#features">Features</a>
        <a href="/#pricing">Pricing</a>
      </div>
    </div>
  </nav>

  <div class="contact-page">
    <div style="text-align:center;margin-bottom:40px;">
      <img src="logo/logo_sideBarBuddy_800px.webp" alt="Sidebar Buddy" style="height:96px;width:auto;border-radius:10px;">
    </div>
    <a href="/" class="back-link">&#8592; Back to home</a>
    <h1>Contact Support</h1>
    <p class="subtitle">Have a question, issue, or license problem? We typically respond within one business day.</p>

    <div class="contact-card">
      <form id="contactForm" novalidate>
        <input type="text" name="website" class="form-honeypot" tabindex="-1" autocomplete="off">

        <div class="form-row">
          <div class="form-group">
            <label for="contactName">Your Name</label>
            <input type="text" id="contactName" placeholder="Jane Smith" required>
          </div>
          <div class="form-group">
            <label for="contactEmail">Your Email</label>
            <input type="email" id="contactEmail" placeholder="jane@example.com" required>
          </div>
        </div>

        <div class="form-group">
          <label for="contactSubject">Topic</label>
          <select id="contactSubject" required>
            <option value="" disabled selected>Select a topic&hellip;</option>
            <option value="I need help with the app">I need help with the app</option>
            <option value="License or activation issue">License or activation issue</option>
            <option value="Refund request">Refund request</option>
            <option value="Suggest a feature">Suggest a feature</option>
            <option value="Report a bug">Report a bug</option>
            <option value="Other">Other</option>
          </select>
        </div>

        <div class="form-group">
          <label for="contactMessage">Message</label>
          <textarea id="contactMessage" rows="6" placeholder="Describe your issue or question in as much detail as you like&hellip;" required></textarea>
        </div>

        <div class="form-submit-row">
          <button type="submit" class="btn-contact" id="contactSubmitBtn">Send Message</button>
          <p class="form-note">No ticket system - just a real reply.</p>
        </div>

        <div id="contactFeedback" class="form-feedback" aria-live="polite"></div>
      </form>

    </div>
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

  <script>
    document.getElementById('contactForm').addEventListener('submit', function(e) {
      e.preventDefault();

      var btn      = document.getElementById('contactSubmitBtn');
      var feedback = document.getElementById('contactFeedback');
      var name     = document.getElementById('contactName').value.trim();
      var email    = document.getElementById('contactEmail').value.trim();
      var subject  = document.getElementById('contactSubject').value;
      var message  = document.getElementById('contactMessage').value.trim();
      var honeypot = this.querySelector('input[name="website"]').value;

      feedback.className = 'form-feedback';
      feedback.textContent = '';

      if (!name || !email || !subject || !message) {
        feedback.className = 'form-feedback error';
        feedback.textContent = 'Please fill in all fields.';
        return;
      }

      btn.disabled = true;
      btn.textContent = 'Sending…';

      fetch('contact_submit.php', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ name: name, email: email, subject: subject, message: message, website: honeypot })
      })
      .then(function(r) { return r.json(); })
      .then(function(data) {
        if (data.success) {
          feedback.className = 'form-feedback success';
          feedback.textContent = "Message sent! We'll get back to you within one business day.";
          document.getElementById('contactForm').reset();
        } else {
          feedback.className = 'form-feedback error';
          feedback.textContent = data.error || 'Something went wrong. Please try again.';
        }
        btn.disabled = false;
        btn.textContent = 'Send Message';
      })
      .catch(function() {
        feedback.className = 'form-feedback error';
        feedback.textContent = 'Network error. Please try again.';
        btn.disabled = false;
        btn.textContent = 'Send Message';
      });
    });
  </script>
</body>
</html>
