-- SQL Server 2022. Run in SQLCMD mode with Execute=0 first.
-- Example: sqlcmd -d NihomeDB -i tools/sqlserver_business_data_purge.sql
--          -v ExpectedDatabase=NihomeDB Execute=0
-- Execute=1 deletes ONLY rows in the explicit table list below.
-- Stop application writes and take a verified database backup before Execute=1.
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> N'$(ExpectedDatabase)'
    THROW 51000, 'Wrong database: check ExpectedDatabase and -d.', 1;

DECLARE @Execute bit = TRY_CONVERT(bit, '$(Execute)');
IF @Execute IS NULL
    THROW 51001, 'Execute must be 0 or 1.', 1;

DECLARE @Targets table (TableName sysname NOT NULL PRIMARY KEY);
INSERT INTO @Targets (TableName) VALUES
    (N'acceptance_records'),
    (N'accounting_corrections'),
    (N'accounting_periods'),
    (N'as_built_documents'),
    (N'basic_design_docs'),
    (N'capability_document_versions'),
    (N'capability_documents'),
    (N'concept_options'),
    (N'construction_task_dependencies'),
    (N'construction_tasks'),
    (N'contract_appendices'),
    (N'contract_attachments'),
    (N'contract_lines'),
    (N'contract_payment_milestone_events'),
    (N'contract_payment_milestones'),
    (N'contracts'),
    (N'customer_activities'),
    (N'customer_contacts'),
    (N'customer_documents'),
    (N'customers'),
    (N'department_finance_reviews'),
    (N'department_work_package_history'),
    (N'department_work_package_participant_groups'),
    (N'department_work_packages'),
    (N'department_work_task_cc_users'),
    (N'department_work_task_dependencies'),
    (N'department_work_tasks'),
    (N'design_projects'),
    (N'design_schedule_history'),
    (N'design_schedule_phases'),
    (N'design_schedule_task_dependencies'),
    (N'design_schedule_tasks'),
    (N'drawing_revisions'),
    (N'handover_records'),
    (N'handover_status_history'),
    (N'hse_violation_events'),
    (N'hse_violations'),
    (N'ifc_release_items'),
    (N'ifc_release_recipients'),
    (N'ifc_releases'),
    (N'kpi_periods'),
    (N'kpi_score_snapshots'),
    (N'lead_activities'),
    (N'leads'),
    (N'material_alert_events'),
    (N'material_alerts'),
    (N'material_rate_catalogs'),
    (N'material_rate_lines'),
    (N'material_rate_revisions'),
    (N'material_request_lines'),
    (N'material_requests'),
    (N'operational_project_assignments'),
    (N'operational_project_member_roles'),
    (N'operational_project_members'),
    (N'operational_project_team_history'),
    (N'operational_projects'),
    (N'opportunities'),
    (N'opportunity_activities'),
    (N'payment_request_attachments'),
    (N'payment_request_events'),
    (N'payment_requests'),
    (N'permit_checklist_items'),
    (N'procurement_input_price_lines'),
    (N'procurement_input_price_offers'),
    (N'procurement_vendors'),
    (N'project_boq_lines'),
    (N'project_boq_revisions'),
    (N'project_documents'),
    (N'project_drive_folders'),
    (N'punch_items'),
    (N'quote_approval_logs'),
    (N'quote_documents'),
    (N'quote_items'),
    (N'quote_version_snapshots'),
    (N'quotes'),
    (N'rfq_award_lines'),
    (N'rfq_award_material_request_allocations'),
    (N'rfq_awards'),
    (N'rfq_bid_lines'),
    (N'rfq_bids'),
    (N'rfq_documents'),
    (N'rfq_events'),
    (N'rfq_invitations'),
    (N'rfq_lines'),
    (N'rfqs'),
    (N'shop_drawings'),
    (N'site_diaries'),
    (N'survey_checklist_results'),
    (N'survey_media'),
    (N'survey_site_conditions'),
    (N'surveys'),
    (N'tender_checklist_items'),
    (N'tender_estimate_lines'),
    (N'tender_estimate_revisions'),
    (N'tenders'),
    (N'vendor_document_uploads'),
    (N'vendor_ratings'),
    (N'warehouse_issue_lines'),
    (N'warehouse_issues'),
    (N'warehouse_receipt_lines'),
    (N'warehouse_receipts'),
    (N'boq_import_templates'),
    (N'tender_checklist_evidence'),
    (N'tender_invitation_documents'),
    (N'tender_milestones'),
    (N'tender_submission_items'),
    (N'tender_submissions'),
    (N'contract_legal_activations'),
    (N'design_review_comments'),
    (N'design_review_documents'),
    (N'design_review_recipients'),
    (N'design_reviews'),
    (N'design_schedule_task_cc_members'),
    (N'permit_checklist_history');

-- These tables are introduced by newer migrations. They may be absent on a
-- local database that has not applied those migrations yet.
DECLARE @Optional table (TableName sysname NOT NULL PRIMARY KEY);
INSERT INTO @Optional (TableName) VALUES
    (N'boq_import_templates'),
    (N'tender_checklist_evidence'),
    (N'tender_invitation_documents'),
    (N'tender_milestones'),
    (N'tender_submission_items'),
    (N'tender_submissions'),
    (N'contract_legal_activations'),
    (N'design_review_comments'),
    (N'design_review_documents'),
    (N'design_review_recipients'),
    (N'design_reviews'),
    (N'design_schedule_task_cc_members'),
    (N'permit_checklist_history');

DECLARE @Retained table (TableName sysname NOT NULL PRIMARY KEY);
INSERT INTO @Retained (TableName) VALUES
    (N'__EFMigrationsHistory'),
    (N'about_section_contents'),
    (N'activities'),
    (N'activity_categories'),
    (N'as_built_document_categories'),
    (N'audit_logs'),
    (N'client_logos'),
    (N'contact_messages'),
    (N'employment_types'),
    (N'entity_translations'),
    (N'google_drive_credentials'),
    (N'hard_delete_items'),
    (N'hard_delete_operations'),
    (N'idempotency_records'),
    (N'job_applications'),
    (N'job_positions'),
    (N'kpi_definitions'),
    (N'master_data_options'),
    (N'news_articles'),
    (N'news_categories'),
    (N'notification_templates'),
    (N'notifications'),
    (N'permissions'),
    (N'process_documents'),
    (N'project_categories'),
    (N'projects'),
    (N'recruitment_dropdown_options'),
    (N'refresh_tokens'),
    (N'registration_otp'),
    (N'role_group_permissions'),
    (N'role_groups'),
    (N'role_permissions'),
    (N'roles'),
    (N'seeded_root_deletions'),
    (N'service_items'),
    (N'site_settings'),
    (N'slideshow_items'),
    (N'translations'),
    (N'user_documents'),
    (N'users'),
    (N'workflow_configs');

IF EXISTS (
    SELECT 1 FROM sys.tables AS table_schema
    WHERE table_schema.is_ms_shipped = 0
      AND (table_schema.schema_id <> SCHEMA_ID(N'dbo')
           OR (NOT EXISTS (SELECT 1 FROM @Targets AS target WHERE target.TableName = table_schema.name)
               AND NOT EXISTS (SELECT 1 FROM @Retained AS retained WHERE retained.TableName = table_schema.name)))
)
BEGIN
    SELECT SCHEMA_NAME(table_schema.schema_id) AS SchemaName,
           table_schema.name AS UnclassifiedTable
    FROM sys.tables AS table_schema
    WHERE table_schema.is_ms_shipped = 0
      AND (table_schema.schema_id <> SCHEMA_ID(N'dbo')
           OR (NOT EXISTS (SELECT 1 FROM @Targets AS target WHERE target.TableName = table_schema.name)
               AND NOT EXISTS (SELECT 1 FROM @Retained AS retained WHERE retained.TableName = table_schema.name)));
    THROW 51004, 'Unclassified tables found. Review scope before proceeding.', 1;
END;

IF EXISTS (
    SELECT 1 FROM sys.foreign_keys AS fk
    JOIN sys.tables AS child ON child.object_id = fk.parent_object_id
    JOIN @Targets AS target ON target.TableName = child.name
    WHERE fk.is_disabled = 1 OR fk.is_not_trusted = 1
)
    THROW 51005, 'Existing target foreign key is disabled or untrusted.', 1;

IF EXISTS (
    SELECT 1 FROM sys.check_constraints AS constraint_schema
    JOIN sys.tables AS table_schema
      ON table_schema.object_id = constraint_schema.parent_object_id
    JOIN @Targets AS target ON target.TableName = table_schema.name
    WHERE constraint_schema.is_disabled = 1 OR constraint_schema.is_not_trusted = 1
)
    THROW 51006, 'Existing target check constraint is disabled or untrusted.', 1;

IF EXISTS (
    SELECT 1 FROM sys.tables AS table_schema
    JOIN @Targets AS target ON target.TableName = table_schema.name
    WHERE table_schema.temporal_type <> 0 OR table_schema.is_tracked_by_cdc = 1
)
    THROW 51007, 'Target table uses temporal history or CDC.', 1;

IF EXISTS (
    SELECT 1 FROM sys.triggers AS trigger_schema
    JOIN sys.tables AS table_schema
      ON table_schema.object_id = trigger_schema.parent_id
    JOIN @Targets AS target ON target.TableName = table_schema.name
)
    THROW 51008, 'Target table has a trigger. Review side effects.', 1;

IF EXISTS (
    SELECT 1 FROM @Targets AS target
    LEFT JOIN sys.tables AS table_schema
        ON table_schema.name = target.TableName
       AND table_schema.schema_id = SCHEMA_ID(N'dbo')
    WHERE table_schema.object_id IS NULL
      AND NOT EXISTS (SELECT 1 FROM @Optional AS optional WHERE optional.TableName = target.TableName)
)
BEGIN
    SELECT target.TableName AS MissingTable
    FROM @Targets AS target
    LEFT JOIN sys.tables AS table_schema
        ON table_schema.name = target.TableName
       AND table_schema.schema_id = SCHEMA_ID(N'dbo')
    WHERE table_schema.object_id IS NULL
      AND NOT EXISTS (SELECT 1 FROM @Optional AS optional WHERE optional.TableName = target.TableName);
    THROW 51002, 'Target table missing. Review schema before proceeding.', 1;
END;

-- Check every retained table that refers to a target table. Such a relation
-- can leave dangling rows or cause a cascading delete outside this list.
IF EXISTS (
    SELECT 1 FROM sys.foreign_keys AS fk
    JOIN sys.tables AS child ON child.object_id = fk.parent_object_id
    JOIN sys.tables AS parent ON parent.object_id = fk.referenced_object_id
    JOIN @Targets AS target ON target.TableName = parent.name
    WHERE parent.schema_id = SCHEMA_ID(N'dbo')
      AND NOT EXISTS (SELECT 1 FROM @Targets AS included WHERE included.TableName = child.name)
)
BEGIN
    SELECT child.name AS RetainedChild, parent.name AS TargetParent, fk.name AS ForeignKeyName
    FROM sys.foreign_keys AS fk
    JOIN sys.tables AS child ON child.object_id = fk.parent_object_id
    JOIN sys.tables AS parent ON parent.object_id = fk.referenced_object_id
    JOIN @Targets AS target ON target.TableName = parent.name
    WHERE parent.schema_id = SCHEMA_ID(N'dbo')
      AND NOT EXISTS (SELECT 1 FROM @Targets AS included WHERE included.TableName = child.name)
    ORDER BY child.name, parent.name;
    THROW 51003, 'Retained tables reference target tables. Review scope.', 1;
END;

SELECT DB_NAME() AS DatabaseName, @Execute AS ExecuteRequested,
       (SELECT COUNT(*) FROM @Targets) AS ListedTargetTables,
       (SELECT COUNT(*) FROM @Targets AS target
        JOIN sys.tables AS table_schema
          ON table_schema.name = target.TableName
         AND table_schema.schema_id = SCHEMA_ID(N'dbo')) AS PresentTargetTables;

SELECT target.TableName, SUM(partition_stats.row_count) AS EstimatedRows
FROM @Targets AS target
JOIN sys.tables AS table_schema
    ON table_schema.name = target.TableName
   AND table_schema.schema_id = SCHEMA_ID(N'dbo')
JOIN sys.dm_db_partition_stats AS partition_stats
    ON partition_stats.object_id = table_schema.object_id
   AND partition_stats.index_id IN (0, 1)
GROUP BY target.TableName
ORDER BY EstimatedRows DESC, target.TableName;

-- Child must be removed before parent while foreign keys are enabled.
SELECT child.name AS DeleteFirst, parent.name AS DeleteAfter, fk.name AS ForeignKeyName
FROM sys.foreign_keys AS fk
JOIN sys.tables AS child ON child.object_id = fk.parent_object_id
JOIN sys.tables AS parent ON parent.object_id = fk.referenced_object_id
JOIN @Targets AS child_target ON child_target.TableName = child.name
JOIN @Targets AS parent_target ON parent_target.TableName = parent.name
WHERE child.schema_id = SCHEMA_ID(N'dbo')
  AND parent.schema_id = SCHEMA_ID(N'dbo')
ORDER BY child.name, parent.name;

IF @Execute = 0 RETURN;

-- Avoid concurrent application writes. All constraint changes and deletes
-- remain inside one transaction; failure rolls back the entire operation.
BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @TableName sysname;
    DECLARE @Sql nvarchar(max);
    DECLARE target_cursor CURSOR LOCAL FAST_FORWARD FOR
        SELECT target.TableName FROM @Targets AS target
        JOIN sys.tables AS table_schema
          ON table_schema.name = target.TableName
         AND table_schema.schema_id = SCHEMA_ID(N'dbo')
        ORDER BY target.TableName;
    OPEN target_cursor;
    FETCH NEXT FROM target_cursor INTO @TableName;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        SET @Sql = N'ALTER TABLE dbo.' + QUOTENAME(@TableName) + N' NOCHECK CONSTRAINT ALL;';
        EXEC sys.sp_executesql @Sql;
        FETCH NEXT FROM target_cursor INTO @TableName;
    END;
    CLOSE target_cursor;

    OPEN target_cursor;
    FETCH NEXT FROM target_cursor INTO @TableName;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        SET @Sql = N'DELETE FROM dbo.' + QUOTENAME(@TableName) + N';';
        EXEC sys.sp_executesql @Sql;
        FETCH NEXT FROM target_cursor INTO @TableName;
    END;
    CLOSE target_cursor;

    OPEN target_cursor;
    FETCH NEXT FROM target_cursor INTO @TableName;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        SET @Sql = N'ALTER TABLE dbo.' + QUOTENAME(@TableName)
            + N' WITH CHECK CHECK CONSTRAINT ALL;';
        EXEC sys.sp_executesql @Sql;
        FETCH NEXT FROM target_cursor INTO @TableName;
    END;
    CLOSE target_cursor;
    DEALLOCATE target_cursor;

    COMMIT TRANSACTION;
    SELECT N'Committed business data deletion' AS Result;
END TRY
BEGIN CATCH
    IF CURSOR_STATUS('local', 'target_cursor') >= 0 CLOSE target_cursor;
    IF CURSOR_STATUS('local', 'target_cursor') > -3 DEALLOCATE target_cursor;
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
