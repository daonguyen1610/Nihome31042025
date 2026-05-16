import { useEffect, useMemo, useState } from "react";
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Switch } from "@/components/ui/switch";
import { useI18n } from "@/lib/i18n";
import type {
  CreateUserRequest,
  RoleMetadataResponse,
  UpdateUserRequest,
  UserDetailResponse,
  UserRole,
} from "@/services/adminApi";

type UserFormModalProps = {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  roles: RoleMetadataResponse[];
  user: UserDetailResponse | null;
  submitting: boolean;
  onSubmit: (payload: CreateUserRequest | UpdateUserRequest) => Promise<void>;
};

type FormState = {
  fullName: string;
  phoneNumber: string;
  email: string;
  password: string;
  role: UserRole | "";
  isActive: boolean;
};

const emptyForm: FormState = {
  fullName: "",
  phoneNumber: "",
  email: "",
  password: "",
  role: "",
  isActive: true,
};

export default function UserFormModal({
  open,
  onOpenChange,
  roles,
  user,
  submitting,
  onSubmit,
}: UserFormModalProps) {
  const { t } = useI18n();
  const [form, setForm] = useState<FormState>(emptyForm);
  const [error, setError] = useState<string | null>(null);
  const isEdit = Boolean(user);

  const defaultRole = useMemo(
    () => roles.find((item) => item.role === "USER")?.role ?? roles[0]?.role ?? "",
    [roles],
  );

  useEffect(() => {
    if (!open) return;

    setError(null);
    setForm(user
      ? {
          fullName: user.fullName ?? "",
          phoneNumber: user.phoneNumber,
          email: user.email ?? "",
          password: "",
          role: user.role,
          isActive: user.isActive,
        }
      : {
          ...emptyForm,
          role: defaultRole,
        });
  }, [defaultRole, open, user]);

  const updateField = <K extends keyof FormState>(key: K, value: FormState[K]) => {
    setForm((prev) => ({ ...prev, [key]: value }));
  };

  const validate = () => {
    if (form.fullName.trim().length < 2) return t("adminUsers.validation.fullName");
    if (!isEdit && form.phoneNumber.trim().length < 8) return t("adminUsers.validation.phone");
    if (!isEdit && form.password.length < 6) return t("adminUsers.validation.password");
    if (!form.role) return t("adminUsers.validation.role");
    return null;
  };

  const submit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const validationError = validate();
    if (validationError) {
      setError(validationError);
      return;
    }

    const email = form.email.trim();
    if (isEdit) {
      await onSubmit({
        fullName: form.fullName.trim(),
        email: email || undefined,
        role: form.role as UserRole,
        isActive: form.isActive,
      });
      return;
    }

    await onSubmit({
      fullName: form.fullName.trim(),
      phoneNumber: form.phoneNumber.trim(),
      email: email || undefined,
      password: form.password,
      role: form.role as UserRole,
    });
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="admin-scope sm:max-w-xl">
        <DialogHeader>
          <DialogTitle className="font-display text-xl font-extrabold">
            {isEdit ? t("adminUsers.editUser") : t("adminUsers.addUser")}
          </DialogTitle>
        </DialogHeader>

        <form onSubmit={submit} className="space-y-4">
          <div>
            <label className="text-xs font-bold uppercase tracking-wider" htmlFor="user-full-name">
              {t("adminUsers.fullName")}
            </label>
            <input
              id="user-full-name"
              value={form.fullName}
              onChange={(event) => updateField("fullName", event.target.value)}
              className="admin-input mt-1 w-full"
              autoComplete="name"
              required
            />
          </div>

          <div>
            <label className="text-xs font-bold uppercase tracking-wider" htmlFor="user-phone">
              {t("adminUsers.phoneNumber")}
            </label>
            <input
              id="user-phone"
              value={form.phoneNumber}
              onChange={(event) => updateField("phoneNumber", event.target.value)}
              className="admin-input mt-1 w-full disabled:opacity-60"
              autoComplete="tel"
              disabled={isEdit}
              required
            />
          </div>

          <div>
            <label className="text-xs font-bold uppercase tracking-wider" htmlFor="user-email">
              {t("adminUsers.email")}
            </label>
            <input
              id="user-email"
              type="email"
              value={form.email}
              onChange={(event) => updateField("email", event.target.value)}
              className="admin-input mt-1 w-full"
              autoComplete="email"
            />
          </div>

          {!isEdit && (
            <div>
              <label className="text-xs font-bold uppercase tracking-wider" htmlFor="user-password">
                {t("adminUsers.password")}
              </label>
              <input
                id="user-password"
                type="password"
                value={form.password}
                onChange={(event) => updateField("password", event.target.value)}
                className="admin-input mt-1 w-full"
                autoComplete="new-password"
                required
              />
            </div>
          )}

          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div>
              <label className="text-xs font-bold uppercase tracking-wider" htmlFor="user-role">
                {t("adminUsers.role")}
              </label>
              <select
                id="user-role"
                value={form.role}
                onChange={(event) => updateField("role", event.target.value as UserRole)}
                className="admin-input mt-1 w-full"
                required
              >
                {roles.map((role) => (
                  <option key={role.role} value={role.role}>
                    {t(role.labelKey)}
                  </option>
                ))}
              </select>
            </div>

            {isEdit && (
              <div className="flex items-center justify-between gap-3 rounded-xl border px-4 py-3 mt-5" style={{ borderColor: "hsl(var(--admin-border))" }}>
                <span className="text-sm font-semibold">{t("adminUsers.active")}</span>
                <Switch
                  checked={form.isActive}
                  onCheckedChange={(checked) => updateField("isActive", checked)}
                  aria-label={t("adminUsers.active")}
                />
              </div>
            )}
          </div>

          {error && <p className="text-sm font-semibold text-destructive">{error}</p>}

          <DialogFooter>
            <button
              type="button"
              className="admin-btn-primary opacity-70"
              onClick={() => onOpenChange(false)}
              disabled={submitting}
            >
              {t("common.cancel")}
            </button>
            <button type="submit" className="admin-btn-primary" disabled={submitting || roles.length === 0}>
              {submitting ? t("common.loading") : isEdit ? t("form.update") : t("form.create")}
            </button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
