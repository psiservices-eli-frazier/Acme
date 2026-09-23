-- Acme schema, SQLite dialect.
--
-- SQLite is the zero-install dev/demo default, standing in for the H2 in-memory
-- database the Java app used. Notes specific to this engine:
--
--   * `INTEGER PRIMARY KEY AUTOINCREMENT` is the only form that gives a monotonic
--     identity column; `BIGINT PRIMARY KEY` would not alias the rowid.
--   * Timestamps are TEXT (ISO-8601). Declaring them TEXT stops SQLite's type
--     affinity from coercing the driver's string form into a number.
--   * Money is NUMERIC. SQLite has no exact decimal type, so this is stored as a
--     double -- see the Phase 0 spike note in CLAUDE.md. The other three dialects
--     get an exact DECIMAL(19,2).
--   * Booleans are INTEGER 0/1.

CREATE TABLE products (
    id             INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    sku            TEXT    NOT NULL,
    name           TEXT    NOT NULL,
    description    TEXT        NULL,
    price          NUMERIC NOT NULL,
    stock_quantity INTEGER NOT NULL,
    active         INTEGER NOT NULL,
    created_at     TEXT    NOT NULL,
    updated_at     TEXT        NULL,
    CONSTRAINT uk_products_sku UNIQUE (sku)
);

CREATE TABLE customers (
    id                  INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    first_name          TEXT    NOT NULL,
    last_name           TEXT    NOT NULL,
    email               TEXT    NOT NULL,
    phone               TEXT        NULL,
    address_line1       TEXT        NULL,
    address_line2       TEXT        NULL,
    address_city        TEXT        NULL,
    address_state       TEXT        NULL,
    address_postal_code TEXT        NULL,
    address_country     TEXT        NULL,
    created_at          TEXT    NOT NULL,
    updated_at          TEXT        NULL,
    CONSTRAINT uk_customers_email UNIQUE (email)
);

CREATE TABLE orders (
    id           INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    order_number TEXT    NOT NULL,
    customer_id  INTEGER NOT NULL,
    status       TEXT    NOT NULL,
    ordered_at   TEXT    NOT NULL,
    notes        TEXT        NULL,
    created_at   TEXT    NOT NULL,
    updated_at   TEXT        NULL,
    CONSTRAINT uk_orders_order_number UNIQUE (order_number),
    CONSTRAINT fk_orders_customer FOREIGN KEY (customer_id) REFERENCES customers (id)
);

-- No unique constraint on (order_id, product_id): the same product may legitimately
-- appear on more than one line of an order.
CREATE TABLE order_items (
    id         INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    order_id   INTEGER NOT NULL,
    product_id INTEGER NOT NULL,
    quantity   INTEGER NOT NULL,
    unit_price NUMERIC NOT NULL,
    CONSTRAINT fk_order_items_order FOREIGN KEY (order_id) REFERENCES orders (id) ON DELETE CASCADE,
    CONSTRAINT fk_order_items_product FOREIGN KEY (product_id) REFERENCES products (id)
);

CREATE TABLE app_users (
    id            INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    username      TEXT    NOT NULL,
    password_hash TEXT    NOT NULL,
    display_name  TEXT    NOT NULL,
    enabled       INTEGER NOT NULL,
    created_at    TEXT    NOT NULL,
    CONSTRAINT uk_app_users_username UNIQUE (username)
);

-- Mirrors the @ElementCollection side table: no surrogate key, one row per role.
CREATE TABLE app_user_roles (
    user_id INTEGER NOT NULL,
    role    TEXT    NOT NULL,
    CONSTRAINT pk_app_user_roles PRIMARY KEY (user_id, role),
    CONSTRAINT fk_app_user_roles_user FOREIGN KEY (user_id) REFERENCES app_users (id) ON DELETE CASCADE
);

-- Indexes Hibernate never generated. The order list sorts on ordered_at by default,
-- and the delete guards count by customer_id / product_id on every delete attempt.
CREATE INDEX ix_orders_customer_id ON orders (customer_id);
CREATE INDEX ix_orders_ordered_at ON orders (ordered_at);
CREATE INDEX ix_order_items_order_id ON order_items (order_id);
CREATE INDEX ix_order_items_product_id ON order_items (product_id);
