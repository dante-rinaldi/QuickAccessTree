-- Sidebar Buddy — Database Schema
-- Run once on the server: mysql -u USER -p DBNAME < schema.sql

CREATE TABLE IF NOT EXISTS licenses (
    id           INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
    order_id     VARCHAR(64)  NOT NULL UNIQUE,
    email        VARCHAR(255) NOT NULL,
    payer_name   VARCHAR(255) NOT NULL DEFAULT '',
    license_key  VARCHAR(32)  NOT NULL UNIQUE,
    type         ENUM('paid','free') NOT NULL DEFAULT 'paid',
    status       ENUM('active','revoked') NOT NULL DEFAULT 'active',
    amount       DECIMAL(8,2) NOT NULL DEFAULT 10.00,
    created_at   DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    INDEX idx_email_key (email, license_key)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS license_activations (
    id           INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
    license_key  VARCHAR(32)  NOT NULL,
    email        VARCHAR(255) NOT NULL,
    device_id    VARCHAR(64)  NOT NULL,
    mac_address  VARCHAR(17)  DEFAULT NULL,
    hostname     VARCHAR(255) DEFAULT NULL,
    ip_address   VARCHAR(45)  DEFAULT NULL,
    country      VARCHAR(100) DEFAULT NULL,
    last_seen    DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    created_at   DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UNIQUE KEY uq_key_device (license_key, device_id),
    INDEX idx_license_key (license_key)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS account_codes (
    id         INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
    email      VARCHAR(255) NOT NULL,
    code       CHAR(6)      NOT NULL,
    attempts   TINYINT UNSIGNED NOT NULL DEFAULT 0,
    expires_at DATETIME     NOT NULL,
    created_at DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    INDEX idx_email (email)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS activation_transfers (
    id          INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
    license_key VARCHAR(32)  NOT NULL,
    email       VARCHAR(255) NOT NULL,
    old_device  VARCHAR(64)  NOT NULL,
    reason      TEXT         DEFAULT NULL,
    status      ENUM('pending','approved','denied') NOT NULL DEFAULT 'pending',
    created_at  DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    resolved_at DATETIME     DEFAULT NULL,
    INDEX idx_license_key (license_key)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS trial_devices (
    id           INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
    device_id    VARCHAR(64)  NOT NULL UNIQUE,
    mac_address  VARCHAR(17)  DEFAULT NULL,
    ip_address   VARCHAR(45)  DEFAULT NULL,
    country      VARCHAR(100) DEFAULT NULL,
    city         VARCHAR(100) DEFAULT NULL,
    launch_count INT UNSIGNED NOT NULL DEFAULT 1,
    first_seen   DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    last_seen    DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
