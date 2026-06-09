CREATE TABLE IF NOT EXISTS stock_quotes
(
    exchange    TEXT        NOT NULL,
    quote_id    TEXT        NOT NULL,
    ticker      TEXT        NOT NULL,
    price       NUMERIC     NOT NULL,
    volume      BIGINT      NOT NULL,
    exchange_ts TIMESTAMPTZ NOT NULL,

    PRIMARY KEY (exchange, quote_id)
);
