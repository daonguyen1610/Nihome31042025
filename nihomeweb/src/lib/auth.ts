// Mock auth for UI demo. Stores user in localStorage. NOT for production.
export type AuthUser = {
  email: string;
  name: string;
  role: "admin" | "user";
};

const KEY = "nicon_demo_user";
const ADMIN_EMAIL = "admin@nicon.vn";

export const getCurrentUser = (): AuthUser | null => {
  try {
    const raw = localStorage.getItem(KEY);
    return raw ? (JSON.parse(raw) as AuthUser) : null;
  } catch {
    return null;
  }
};

export const login = (email: string, _password: string): AuthUser => {
  const role: AuthUser["role"] = email.toLowerCase() === ADMIN_EMAIL ? "admin" : "user";
  const user: AuthUser = {
    email,
    name: email.split("@")[0],
    role,
  };
  localStorage.setItem(KEY, JSON.stringify(user));
  return user;
};

export const register = (name: string, email: string, _password: string): AuthUser => {
  const role: AuthUser["role"] = email.toLowerCase() === ADMIN_EMAIL ? "admin" : "user";
  const user: AuthUser = { name, email, role };
  localStorage.setItem(KEY, JSON.stringify(user));
  return user;
};

export const logout = () => {
  localStorage.removeItem(KEY);
};

export const ADMIN_HINT = ADMIN_EMAIL;
