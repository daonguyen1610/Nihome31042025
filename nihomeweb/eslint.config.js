import js from "@eslint/js";
import globals from "globals";
import reactHooks from "eslint-plugin-react-hooks";
import reactRefresh from "eslint-plugin-react-refresh";
import tseslint from "typescript-eslint";

export default tseslint.config(
  { ignores: ["dist", "playwright-report", "test-results", "e2e"] },
  {
    extends: [js.configs.recommended, ...tseslint.configs.recommended],
    files: ["**/*.{ts,tsx}"],
    languageOptions: {
      ecmaVersion: 2020,
      globals: globals.browser,
    },
    plugins: {
      "react-hooks": reactHooks,
      "react-refresh": reactRefresh,
    },
    rules: {
      ...reactHooks.configs.recommended.rules,
      "react-refresh/only-export-components": ["warn", { allowConstantExport: true }],
      "@typescript-eslint/no-unused-vars": "off",
      "no-restricted-properties": [
        "error",
        {
          object: "crypto",
          property: "randomUUID",
          message:
            "crypto.randomUUID is missing on pages served over plain http (production). Use randomId() from @/lib/randomId.",
        },
      ],
    },
  },
  {
    // The two places that may call it: both check it exists first.
    files: ["src/lib/randomId.ts", "src/lib/logger.ts"],
    rules: { "no-restricted-properties": "off" },
  },
  {
    files: [
      "src/components/ui/**/*.{ts,tsx}",
      "src/lib/**/*.{ts,tsx}",
      "src/store/**/*.{ts,tsx}",
      "src/services/**/*.{ts,tsx}",
    ],
    rules: {
      "react-refresh/only-export-components": "off",
    },
  },
);
