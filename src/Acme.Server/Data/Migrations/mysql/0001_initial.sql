-- Acme schema, MySQL dialect.
--
-- InnoDB throughout, because the delete guards and the order/line relationship rely
-- on real foreign keys. DATETIME(6) rather than DATETIME so the timestamps keep the
-- sub-second precision LocalDateTime carried.

CREATE TABLE products (
    id             BIGINT         NOT NULL AUTO_INCREMENT,
    sku            VARCHAR(64)    NOT NULL,
    name           VARCHAR(200)   NOT NULL,
    description    VARCHAR(2000)      NULL,
    price          DECIMAL(19, 2) NOT NULL,
    stock_quantity INT            NOT NULL,
    active         TINYINT(1)     NOT NULL,
    created_at     DATETIME(6)    NOT NULL,
    updated_at     DATETIME(6)        NULL,
    PRIMARY KEY (id),
    CONSTRAINT uk_products_sku UNIQUE (sku)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4;

CREATE TABLE customers (
    id                  BIGINT       NOT NULL AUTO_INCREMENT,
    first_name          VARCHAR(100) NOT NULL,
    last_name           VARCHAR(100) NOT NULL,
    email               VARCHAR(255) NOT NULL,
    phone               VARCHAR(40)      NULL,
    address_line1       VARCHAR(200)     NULL,
    address_line2       VARCHAR(200)     NULL,
    address_city        VARCHAR(100)     NULL,
    address_state       VARCHAR(100)     NULL,
    address_postal_code VARCHAR(20)      NULL,
    address_country     VARCHAR(100)     NULL,
    created_at          DATETIME(6)  NOT NULL,
    updated_at          DATETIME(6)      NULL,
    PRIMARY KEY (id),
    CONSTRAINT uk_customers_email UNIQUE (email)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4;

CREATE TABLE orders (
    id           BIGINT        NOT NULL AUTO_INCREMENT,
    order_number VARCHAR(40)   NOT NULL,
    customer_id  BIGINT        NOT NULL,
    status       VARCHAR(20)   NOT NULL,
    ordered_at   DATETIME(6)   NOT NULL,
    notes        VARCHAR(2000)     NULL,
    created_at   DATETIME(6)   NOT NULL,
    updated_at   DATETIME(6)       NULL,
    PRIMARY KEY (id),
    CONSTRAINT uk_orders_order_number UNIQUE (order_number),
    CONSTRAINT fk_orders_customer FOREIGN KEY (customer_id) REFERENCES customers (id)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4;

-- No unique constraint on (order_id, product_id): the same product may legitimately
-- appear on more than one line of an order.
CREATE TABLE order_items (
    id         BIGINT         NOT NULL AUTO_INCREMENT,
    order_id   BIGINT         NOT NULL,
    product_id BIGINT         NOT NULL,
    quantity   INT            NOT NULL,
    unit_price DECIMAL(19, 2) NOT NULL,
    PRIMARY KEY (id),
    CONSTRAINT fk_order_items_order FOREIGN KEY (order_id) REFERENCES orders (id) ON DELETE CASCADE,
    CONSTRAINT fk_order_items_product FOREIGN KEY (product_id) REFERENCES products (id)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4;

CREATE TABLE app_users (
    id            BIGINT       NOT NULL AUTO_INCREMENT,
    username      VARCHAR(64)  NOT NULL,
    password_hash VARCHAR(200) NOT NULL,
    display_name  VARCHAR(120) NOT NULL,
    enabled       TINYINT(1)   NOT NULL,
    created_at    DATETIME(6)  NOT NULL,
    PRIMARY KEY (id),
    CONSTRAINT uk_app_users_username UNIQUE (username)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4;

-- Mirrors the @ElementCollection side table: no surrogate key, one row per role.
CREATE TABLE app_user_roles (
    user_id BIGINT      NOT NULL,
    role    VARCHAR(32) NOT NULL,
    PRIMARY KEY (user_id, role),
    CONSTRAINT fk_app_user_roles_user FOREIGN KEY (user_id) REFERENCES app_users (id) ON DELETE CASCADE
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4;

-- MySQL indexes foreign key columns automatically, so unlike the other three dialects
-- only the sort index is needed here: the order list sorts on ordered_at by default.
CREATE INDEX ix_orders_ordered_at ON orders (ordered_at);
