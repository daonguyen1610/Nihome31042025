import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";
import ts from "typescript";

const source = await readFile(new URL("../src/lib/people.ts", import.meta.url), "utf8");
const compiled = ts.transpileModule(source, {
  compilerOptions: { module: ts.ModuleKind.ES2022, target: ts.ScriptTarget.ES2022 },
}).outputText;
const { personOption, projectMemberOption, mergePeople, collectPeoplePages } = await import(
  "data:text/javascript;base64," + Buffer.from(compiled).toString("base64")
);
const t = (key) =>
  ({
    "role.architect": "Architect",
    "department.design": "Design",
    "designProjects.team.role.DesignLead": "Design lead",
  })[key] ?? key;

test("schedule choices preserve membership IDs while directory choices use user IDs", () => {
  const member = {
    id: 142,
    userId: 17,
    userName: "Nguyễn Minh An",
    roleName: "Kiến trúc sư",
    roleLabelKey: "role.architect",
    departmentId: 3,
    departmentLabelKey: "department.design",
    position: "Concept coordination",
    roles: [{ roleCode: "DesignLead" }],
  };
  const schedule = projectMemberOption(member, t);
  assert.equal(schedule.value, "142");
  assert.equal(personOption(member, t).value, "17");
  assert.equal(schedule.department.value, "3");
  assert.equal(schedule.label, "Nguyễn Minh An");
  assert.match(schedule.hint, /Architect/);
  assert.match(schedule.hint, /Design lead/);
  assert.match(schedule.hint, /Concept coordination/);
});

test("directory pagination includes later pages and inactive historical people without duplicates", async () => {
  const people = Array.from({ length: 201 }, (_, index) => ({
    id: index + 1,
    isActive: index !== 150,
  }));
  const requests = [];
  const result = await collectPeoplePages(
    { items: people.slice(0, 100), total: 202 },
    async (skip, take) => {
      requests.push({ skip, take });
      return {
        items: skip === 100 ? people.slice(100, 200) : [people[200], people[0]],
        total: 202,
      };
    },
  );
  assert.equal(result.length, 201);
  assert.deepEqual(requests, [
    { skip: 100, take: 100 },
    { skip: 200, take: 100 },
  ]);
  assert.equal(result.find((person) => person.id === 151).isActive, false);
  assert.equal(result.at(-1).id, 201);
});

test("later directory request failures propagate rather than return a partial candidate list", async () => {
  const failure = new Error("Directory unavailable");
  await assert.rejects(
    collectPeoplePages({ items: [{ id: 1 }], total: 201 }, async () => {
      throw failure;
    }),
    (error) => error === failure,
  );
});

test("partial identity does not claim a confirmed unassigned department", () => {
  const unknown = personOption({ id: 17, name: "An" }, t);
  const unassigned = personOption({ id: 17, name: "An", departmentId: null }, t);
  assert.equal(unknown.department.label, "people.departmentUnknown");
  assert.equal(unassigned.department.label, "people.departmentUnassigned");
  assert.notEqual(unknown.department.value, unassigned.department.value);
});

test("stored recipient-name fallbacks retain eligible candidate role and department", () => {
  const [person] = mergePeople([
    { userId: 17, name: "Nguyễn Minh An", roleName: "Architect", departmentId: 3 },
    { userId: 17, name: "Nguyễn Minh An" },
  ]);
  assert.equal(person.roleName, "Architect");
  assert.equal(person.departmentId, 3);
});
