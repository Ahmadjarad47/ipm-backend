namespace Ipm.Infrastructure.Persistence.Auditing;

/// <summary>
/// Part 08 R5 to R7. The audit log is append-only and hash-chained inside the database, so no application bug can skip either: a BEFORE INSERT
/// trigger takes a transaction lock, numbers the row, links it to the previous one and seals it; UPDATE, DELETE and TRUNCATE are refused.
/// Used by the migration and by the test database (which is created from the model).
/// </summary>
public static class AuditChainSql
{
    public const string Functions = """
        CREATE OR REPLACE FUNCTION public.audit_log_payload(
            p_seq bigint, p_entity text, p_entity_id uuid, p_action text, p_before jsonb, p_after jsonb, p_user_id uuid, p_at timestamptz,
            p_project_id uuid, p_work_item_id uuid, p_label text, p_source text, p_correlation text)
        RETURNS text LANGUAGE sql IMMUTABLE AS $$
            SELECT concat_ws('|', p_seq, p_entity, p_entity_id, p_action, coalesce(p_before::text, ''), coalesce(p_after::text, ''),
                coalesce(p_user_id::text, ''), to_char(p_at AT TIME ZONE 'UTC', 'YYYY-MM-DD"T"HH24:MI:SS.US'),
                coalesce(p_project_id::text, ''), coalesce(p_work_item_id::text, ''), coalesce(p_label, ''), p_source, p_correlation)
        $$;

        CREATE OR REPLACE FUNCTION public.audit_log_chain() RETURNS trigger LANGUAGE plpgsql AS $$
        DECLARE last_seq bigint; last_hash text;
        BEGIN
            PERFORM pg_advisory_xact_lock(7770001);
            SELECT seq, hash INTO last_seq, last_hash FROM public.audit_log ORDER BY seq DESC LIMIT 1;
            NEW.seq := coalesce(last_seq, 0) + 1;
            NEW.prev_hash := coalesce(last_hash, '');
            NEW.hash := encode(sha256(convert_to(NEW.prev_hash || public.audit_log_payload(
                NEW.seq, NEW.entity, NEW.entity_id, NEW.action, NEW.before, NEW.after, NEW.user_id, NEW.at,
                NEW.project_id, NEW.work_item_id, NEW.subject_label, NEW.source, NEW.correlation_id), 'UTF8')), 'hex');
            RETURN NEW;
        END $$;

        CREATE OR REPLACE FUNCTION public.audit_log_immutable() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN
            RAISE EXCEPTION 'audit_log is append-only';
        END $$;
        """;

    public const string Triggers = """
        DROP TRIGGER IF EXISTS audit_log_chain ON public.audit_log;
        CREATE TRIGGER audit_log_chain BEFORE INSERT ON public.audit_log FOR EACH ROW EXECUTE FUNCTION public.audit_log_chain();
        DROP TRIGGER IF EXISTS audit_log_no_change ON public.audit_log;
        CREATE TRIGGER audit_log_no_change BEFORE UPDATE OR DELETE ON public.audit_log FOR EACH ROW EXECUTE FUNCTION public.audit_log_immutable();
        DROP TRIGGER IF EXISTS audit_log_no_truncate ON public.audit_log;
        CREATE TRIGGER audit_log_no_truncate BEFORE TRUNCATE ON public.audit_log FOR EACH STATEMENT EXECUTE FUNCTION public.audit_log_immutable();
        """;

    /// <summary>Everything the database needs for the chain (the migration runs the two halves around the backfill).</summary>
    public static readonly string Install = Functions + Environment.NewLine + Triggers;

    /// <summary>Gives the rows that existed before part 08 their place in the chain (ordered by time then id). Run once by the migration, between the functions and the triggers.</summary>
    public const string Backfill = """
        DO $$
        DECLARE r record; n bigint := 0; prev text := ''; h text;
        BEGIN
            FOR r IN SELECT * FROM public.audit_log ORDER BY at, id LOOP
                n := n + 1;
                h := encode(sha256(convert_to(prev || public.audit_log_payload(
                    n, r.entity, r.entity_id, r.action, r.before, r.after, r.user_id, r.at, NULL::uuid, NULL::uuid, NULL::text, 'system'::text, ''::text), 'UTF8')), 'hex');
                UPDATE public.audit_log SET seq = n, prev_hash = prev, hash = h, source = 'system' WHERE id = r.id;
                prev := h;
            END LOOP;
        END $$;
        """;
}
