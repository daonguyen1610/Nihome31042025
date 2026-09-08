import { execFileSync } from "node:child_process";

/**
 * SQL access for isolated E2E fixture setup/teardown that has no public API.
 * Configure E2E_SQL_CONTAINER and E2E_SQL_DATABASE alongside BASE_URL when
 * running a separate validation stack. The password stays inside the SQL
 * container, using SQLCMDPASSWORD or its existing MSSQL_SA_PASSWORD/SA_PASSWORD.
 */
const SQL_CONTAINER = process.env.E2E_SQL_CONTAINER ?? "nihome31042025-sqlserver";
const SQL_USER = process.env.E2E_SQL_USER ?? "sa";
const SQL_DATABASE = process.env.E2E_SQL_DATABASE ?? "NihomeDB";

export function execSql(sql: string): string {
  const args = [
    "exec",
    SQL_CONTAINER,
    "sh", "-c",
    'export SQLCMDPASSWORD="${SQLCMDPASSWORD:-${MSSQL_SA_PASSWORD:-$SA_PASSWORD}}"; exec "$@"',
    "e2e-sql",
    "/opt/mssql-tools18/bin/sqlcmd",
    "-S", "localhost",
    "-U", SQL_USER,
    "-d", SQL_DATABASE,
    "-C",
    "-b",
    "-h", "-1",
    "-W",
    "-Q", sql,
  ];
  return execFileSync("docker", args, { encoding: "utf-8" });
}
