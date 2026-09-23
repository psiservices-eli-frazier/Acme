-- Acme schema, SQL Server dialect.
--
-- NVARCHAR throughout (the Java columns are Unicode-capable), DATETIME2 for the
-- timestamps, BIT for the booleans.

CREATE TABLE products (
    id             BIGINT         NOT NULL IDENTITY (1, 1) PRIMARY KEY,
    sku            NVARCHAR(64)   NOT NULL,
    name           NVARCHAR(200)  NOT NULL,
    description    NVARCHAR(2000)     NULL,
    price          DECIMAL(19, 2) NOT NULL,
    stock_quantity INT            NOT NULL,
    active         BIT            NOT NULL,
    created_at     DATETIME2      NOT NULL,
    updated_at     DATETIME2          NULL,
    CONSTRAINT uk_products_sku UNIQUE (sku)
);

CREATE TABLE customers (
    id                  BIGINT        NOT NULL IDENTITY (1, 1) PRIMARY KEY,
    first_name          NVARCHAR(100) NOT NULL,
    last_name           NVARCHAR(100) NOT NULL,
    email               NVARCHAR(255) NOT NULL,
    phone               NVARCHAR(40)      NULL,
    address_line1       NVARCHAR(200)     NULL,
    address_line2       NVARCHAR(200)     NULL,
    address_city        NVARCHAR(100)     NULL,
    address_state       NVARCHAR(100)     NULL,
    address_postal_code NVARCHAR(20)      NULL,
    address_country     NVARCHAR(100)     NULL,
    created_at          DATETIME2     NOT NULL,
    updated_at          DATETIME2         NULL,
    CONSTRAINT uk_customers_email UNIQUE (email)
);

CREATE TABLE orders (
    id           BIGINT         NOT NULL IDENTITY (1, 1) PRIMARY KEY,
    order_number NVARCHAR(40)   NOT NULL,
    customer_id  BIGINT         NOT NULL,
    status       NVARCHAR(20)   NOT NULL,
    ordered_at   DATETIME2      NOT NULL,
    notes        NVARCHAR(2000)     NULL,
    created_at   DATETIME2      NOT NULL,
    updated_at   DATETIME2          NULL,
    CONSTRAINT uk_orders_order_number UNIQUE (order_number),
    CONSTRAINT fk_orders_customer FOREIGN KEY (customer_id) REFERENCES customers (id)
);

-- No unique constraint on (order_id, product_id): the same product may legitimately
-- appear on more than one line of an order.
CREATE TABLE order_items (
    id         BIGINT         NOT NULL IDENTITY (1, 1) PRIMARY KEY,
    order_id   BIGINT         NOT NULL,
    product_id BIGINT         NOT NULL,
    quantity   INT            NOT NULL,
    unit_price DECIMAL(19, 2) NOT NULL,
    CONSTRAINT fk_order_items_order FOREIGN KEY (order_id) REFERENCES orders (id) ON DELETE CASCADE,
    CONSTRAINT fk_order_items_product FOREIGN KEY (product_id) REFERENCES products (id)
);

CREATE TABLE app_users (
    id            BIGINT        NOT NULL IDENTITY (1, 1) PRIMARY KEY,
    username      NVARCHAR(64)  NOT NULL,
    password_hash NVARCHAR(200) NOT NULL,
    display_name  NVARCHAR(120) NOT NULL,
    enabled       BIT           NOT NULL,
    created_at    DATETIME2     NOT NULL,
    CONSTRAINT uk_app_users_username UNIQUE (username)
);

-- Mirrors the @ElementCollection side table: no surrogate key, one row per role.
CREATE TABLE app_user_roles (
    user_id BIGINT       NOT NULL,
    role    NVARCHAR(32) NOT NULL,
    CONSTRAINT pk_app_user_roles PRIMARY KEY (user_id, role),
    CONSTRAINT fk_app_user_roles_user FOREIGN KEY (user_id) REFERENCES app_users (id) ON DELETE CASCADE
);

-- Indexes Hibernate never generated. The order list sorts on ordered_at by default,
-- and the delete guards count by customer_id / product_id on every delete attempt.
CREATE INDEX ix_orders_customer_id ON orders (customer_id);
CREATE INDEX ix_orders_ordered_at ON orders (ordered_at);
CREATE INDEX ix_order_items_order_id ON order_items (order_id);
CREATE INDEX ix_order_items_product_id ON order_items (product_id);
