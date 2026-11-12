import { execFileSync } from "node:child_process";

/**
 * SQL access for isolated E2E fixture setup/teardown that has no public API.
 * Set E2E_SQL_CONTAINER and E2E_SQL_DATABASE alongside BASE_URL explicitly.
 * There is deliberately no default: a missing value must never redirect test
 * setup or cleanup to a developer's shared database. The password stays inside the SQL
 * container, using SQLCMDPASSWORD or its existing MSSQL_SA_PASSWORD/SA_PASSWORD.
 * -I enables QUOTED_IDENTIFIER for writes to tables with filtered indexes.
 */
const SQL_CONTAINER = process.env.E2E_SQL_CONTAINER;
const SQL_USER = process.env.E2E_SQL_USER ?? "sa";
const SQL_DATABASE = process.env.E2E_SQL_DATABASE ?? "NihomeDB";

export function execSql(sql: string): string {
  if (!SQL_CONTAINER) {
    throw new Error(
      "E2E_SQL_CONTAINER is required for SQL-backed E2E fixtures. Set it to the test stack's SQL container name.",
    );
  }

  const args = [
    "exec",
    SQL_CONTAINER,
    "sh",
    "-c",
    'export SQLCMDPASSWORD="${SQLCMDPASSWORD:-${MSSQL_SA_PASSWORD:-$SA_PASSWORD}}"; exec "$@"',
    "e2e-sql",
    "/opt/mssql-tools18/bin/sqlcmd",
    "-S",
    "localhost",
    "-U",
    SQL_USER,
    "-d",
    SQL_DATABASE,
    "-C",
    "-b",
    "-I",
    "-h",
    "-1",
    "-W",
    "-Q",
    sql,
  ];
  return execFileSync("docker", args, { encoding: "utf-8" });
}
