import { execSync } from "node:child_process";

/**
 * Minimal SQL helper for E2E setup/teardown.
 *
 * Runs sqlcmd inside the docker-compose stack's MSSQL container so the test
 * file doesn't need a Node SQL driver. Only used to wire test fixtures that
 * the public API does not expose (e.g. assigning a user to a business role
 * via users.RoleEntityId).
 *
 * Container name + sa password are pinned to docker-compose.yaml.
 */
const SQL_CONTAINER = "nihome31042025-sqlserver";
const SQL_USER = "sa";
const SQL_PASSWORD = "Nihome@31042025";
const SQL_DATABASE = "NihomeDB";

export function execSql(sql: string): string {
  const cmd = [
    "docker exec",
    SQL_CONTAINER,
    "/opt/mssql-tools18/bin/sqlcmd",
    "-S localhost",
    "-U", SQL_USER,
    "-P", `'${SQL_PASSWORD}'`,
    "-d", SQL_DATABASE,
    "-C",
    "-h -1",
    "-W",
    "-Q", JSON.stringify(sql),
  ].join(" ");
  return execSync(cmd, { encoding: "utf-8" });
}
