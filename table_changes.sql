-- 创建止盈策略表
CREATE TABLE `take_profit_strategies` (
    `id` BIGINT(19) NOT NULL AUTO_INCREMENT,
    `order_id` VARCHAR(36) NOT NULL COMMENT '关联订单UUID',
    `strategy_type` VARCHAR(20) NOT NULL COMMENT '策略类型：fixed_price-固定价格, drawdown-回撤百分比, profit_trigger-浮盈触发, breakeven-保本止盈',
    `trigger_price` DECIMAL(20,4) NULL COMMENT '触发价格（用于固定价格策略）',
    `drawdown_percentage` DECIMAL(5,2) NULL COMMENT '回撤百分比（用于回撤策略）',
    `profit_trigger_amount` DECIMAL(20,2) NULL COMMENT '浮盈触发金额（用于浮盈触发策略）',
    `profit_fallback_amount` DECIMAL(20,2) NULL COMMENT '浮盈回落金额（用于浮盈触发策略）',
    `breakeven_profit_amount` DECIMAL(20,2) NULL COMMENT '保本触发金额（用于保本止盈策略）',
    `status` VARCHAR(20) NOT NULL DEFAULT 'active' COMMENT '策略状态：active-生效中, triggered-已触发, cancelled-已取消',
    `created_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    `updated_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT '更新时间',
    PRIMARY KEY (`id`) USING BTREE,
    INDEX `idx_order_id` (`order_id`) USING BTREE,
    CONSTRAINT `fk_take_profit_strategies_order` FOREIGN KEY (`order_id`) REFERENCES `simulation_orders` (`order_id`) ON UPDATE CASCADE ON DELETE CASCADE
) COMMENT='订单止盈策略表'
COLLATE='utf8mb4_0900_ai_ci'
ENGINE=InnoDB;

CREATE TABLE `accounts` (
	`id` BIGINT(19) NOT NULL AUTO_INCREMENT,
	`name` VARCHAR(50) NOT NULL COMMENT '账户名称' COLLATE 'utf8mb4_0900_ai_ci',
	`equity` DECIMAL(20,2) NULL DEFAULT NULL COMMENT '当前权益',
	`init_value` DECIMAL(20,2) NULL DEFAULT NULL COMMENT '初始资金',
	`single_order_risk` DECIMAL(10,2) NULL DEFAULT '5000.00' COMMENT '单笔订单风险金',
	`status` VARCHAR(20) NULL DEFAULT 'active' COMMENT '账户状态：active-活跃, frozen-冻结, closed-关闭' COLLATE 'utf8mb4_0900_ai_ci',
	`created_at` DATETIME NULL DEFAULT 'CURRENT_TIMESTAMP',
	`updated_at` DATETIME NULL DEFAULT 'CURRENT_TIMESTAMP' ON UPDATE CURRENT_TIMESTAMP,
	`max_total_risk` DECIMAL(10,2) NULL DEFAULT '0.00' COMMENT '总风险金',
	PRIMARY KEY (`id`) USING BTREE,
	UNIQUE INDEX `name` (`name`) USING BTREE
)
COMMENT='账户基本信息表'
COLLATE='utf8mb4_0900_ai_ci'
ENGINE=InnoDB
AUTO_INCREMENT=4
;


-- 修改订单表
ALTER TABLE `simulation_orders`
    DROP COLUMN `take_profit_price`,
    ADD COLUMN `highest_price` DECIMAL(20,4) NULL COMMENT '订单期间最高价格（用于回撤策略）' AFTER `current_stop_loss`,
    ADD COLUMN `max_floating_profit` DECIMAL(20,2) NULL COMMENT '最大浮动盈利（用于浮盈触发策略）' AFTER `highest_price`,
    MODIFY COLUMN `close_type` VARCHAR(20) NULL COMMENT '平仓类型：take_profit_fixed-固定价格止盈, take_profit_drawdown-回撤止盈, take_profit_trigger-浮盈触发止盈, take_profit_breakeven-保本止盈, stop_loss-止损, manual-手动' AFTER `realized_profit`;

CREATE TABLE `simulation_orders` (
	`id` BIGINT(19) NOT NULL AUTO_INCREMENT,
	`order_id` VARCHAR(36) NOT NULL COMMENT '订单UUID' COLLATE 'utf8mb4_0900_ai_ci',
	`account_id` BIGINT(19) NOT NULL COMMENT '关联账户ID',
	`contract` VARCHAR(20) NOT NULL COMMENT '合约名称' COLLATE 'utf8mb4_0900_ai_ci',
	`direction` VARCHAR(10) NOT NULL COMMENT '方向：buy-买入, sell-卖出' COLLATE 'utf8mb4_0900_ai_ci',
	`quantity` INT(10) NOT NULL COMMENT '持仓数量',
	`entry_price` DECIMAL(20,4) NOT NULL COMMENT '开仓价格',
	`initial_stop_loss` DECIMAL(20,4) NOT NULL COMMENT '止损价格',
	`current_stop_loss` DECIMAL(20,4) NOT NULL COMMENT '当前止损价格',
	`highest_price` DECIMAL(20,4) NULL DEFAULT NULL COMMENT '订单期间最高价格（用于回撤策略）',
	`max_floating_profit` DECIMAL(20,2) NULL DEFAULT NULL COMMENT '最大浮动盈利（用于浮盈触发策略）',
	`leverage` INT(10) NOT NULL DEFAULT '10' COMMENT '杠杆倍数',
	`margin` DECIMAL(20,2) NOT NULL COMMENT '保证金',
	`total_value` DECIMAL(20,2) NOT NULL COMMENT '总市值',
	`status` VARCHAR(20) NOT NULL COMMENT '状态：open-持仓中, pending-挂单中, closed-已平仓' COLLATE 'utf8mb4_0900_ai_ci',
	`open_time` DATETIME NOT NULL COMMENT '开仓时间',
	`close_time` DATETIME NULL DEFAULT NULL COMMENT '平仓时间',
	`close_price` DECIMAL(20,4) NULL DEFAULT NULL COMMENT '平仓价格',
	`realized_profit` DECIMAL(20,2) NULL DEFAULT NULL COMMENT '已实现盈亏',
	`close_type` VARCHAR(20) NULL DEFAULT NULL COMMENT '平仓类型：take_profit_fixed-固定价格止盈, take_profit_drawdown-回撤止盈, take_profit_trigger-浮盈触发止盈, take_profit_breakeven-保本止盈, stop_loss-止损, manual-手动' COLLATE 'utf8mb4_0900_ai_ci',
	PRIMARY KEY (`id`) USING BTREE,
	UNIQUE INDEX `uk_order_id` (`order_id`) USING BTREE,
	INDEX `idx_account_status` (`account_id`, `status`) USING BTREE,
	CONSTRAINT `fk_simulation_orders_account` FOREIGN KEY (`account_id`) REFERENCES `accounts` (`id`) ON UPDATE NO ACTION ON DELETE NO ACTION
)
COMMENT='模拟交易订单表'
COLLATE='utf8mb4_0900_ai_ci'
ENGINE=InnoDB
AUTO_INCREMENT=8
;
