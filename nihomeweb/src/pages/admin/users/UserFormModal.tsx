import { useEffect, useMemo, useState } from "react";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Switch } from "@/components/ui/switch";
import { useI18n } from "@/lib/i18n";
import type {
  CreateUserRequest,
  UpdateUserRequest,
  UserDetailResponse,
} from "@/services/adminApi";
import type { RoleResponse } from "@/services/rbacApi";

type UserFormModalProps = {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  /** Full RBAC role catalog (system + custom). Inactive roles are filtered by the parent. */
  roles: RoleResponse[];
  user: UserDetailResponse | null;
  submitting: boolean;
  onSubmit: (payload: CreateUserRequest | UpdateUserRequest) => Promise<void>;
};

type FormState = {
  fullName: string;
  phoneNumber: string;
  email: string;
  password: string;
  /** Selected RBAC role code (e.g. "ADMIN", "PROJECT_MANAGER"). */
  role: string;
  isActive: boolean;
};

const PUBLIC_ROLE_CODE = "USER";

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
    () => roles.find((item) => item.code === PUBLIC_ROLE_CODE)?.code ?? roles[0]?.code ?? "",
    [roles],
  );

  // RoleResponse exposes both a backend-supplied display name and an optional
  // i18n key. Prefer the translation if a key is present and resolves; otherwise
  // fall back to the seed-time name so custom roles without translations still
  // render usefully.
  const renderRoleLabel = (role: RoleResponse): string => {
    if (role.labelKey) {
      const translated = t(role.labelKey);
      if (translated && translated !== role.labelKey) return translated;
    }
    return role.name;
  };

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
    if (!email) {
      setError(t("adminUsers.validation.email") || t("profile.emailRequired") || "Vui lòng nhập email");
      return;
    }

    if (isEdit) {
      await onSubmit({
        fullName: form.fullName.trim(),
        email,
        role: form.role,
        isActive: form.isActive,
      });
      return;
    }

    await onSubmit({
      fullName: form.fullName.trim(),
      phoneNumber: form.phoneNumber.trim(),
      email,
      password: form.password,
      role: form.role,
    });
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-xl">
        <DialogHeader>
          <DialogTitle>
            {isEdit ? t("adminUsers.editUser") : t("adminUsers.addUser")}
          </DialogTitle>
        </DialogHeader>

        <form onSubmit={submit} className="space-y-4">
          <div className="space-y-1.5">
            <Label className="text-xs" htmlFor="user-full-name">{t("adminUsers.fullName")}</Label>
            <Input
              id="user-full-name"
              value={form.fullName}
              onChange={(event) => updateField("fullName", event.target.value)}
              autoComplete="name"
              required
            />
          </div>

          <div className="space-y-1.5">
            <Label className="text-xs" htmlFor="user-phone">{t("adminUsers.phoneNumber")}</Label>
            <Input
              id="user-phone"
              value={form.phoneNumber}
              onChange={(event) => updateField("phoneNumber", event.target.value)}
              autoComplete="tel"
              disabled={isEdit}
              required
            />
          </div>

          <div className="space-y-1.5">
            <Label className="text-xs" htmlFor="user-email">{t("adminUsers.email")}</Label>
            <Input
              id="user-email"
              type="email"
              value={form.email}
              onChange={(event) => updateField("email", event.target.value)}
              autoComplete="email"
              required
            />
          </div>

          {!isEdit && (
            <div className="space-y-1.5">
              <Label className="text-xs" htmlFor="user-password">{t("adminUsers.password")}</Label>
              <Input
                id="user-password"
                type="password"
                value={form.password}
                onChange={(event) => updateField("password", event.target.value)}
                autoComplete="new-password"
                required
              />
            </div>
          )}

          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div className="space-y-1.5">
              <Label className="text-xs" htmlFor="user-role">{t("adminUsers.role")}</Label>
              <Select
                value={form.role}
                onValueChange={(v) => updateField("role", v)}
              >
                <SelectTrigger id="user-role">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {roles.map((role) => (
                    <SelectItem key={role.code} value={role.code}>
                      {renderRoleLabel(role)}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            {isEdit && (
              <div className="mt-6 flex items-center justify-between gap-3 rounded-md border px-4 py-3">
                <span className="text-sm font-medium">{t("adminUsers.active")}</span>
                <Switch
                  checked={form.isActive}
                  onCheckedChange={(checked) => updateField("isActive", checked)}
                  aria-label={t("adminUsers.active")}
                />
              </div>
            )}
          </div>

          {error && <p className="text-sm font-medium text-destructive">{error}</p>}

          <DialogFooter>
            <Button
              type="button"
              variant="outline"
              onClick={() => onOpenChange(false)}
              disabled={submitting}
            >
              {t("common.cancel")}
            </Button>
            <Button type="submit" disabled={submitting || roles.length === 0}>
              {submitting ? t("common.loading") : isEdit ? t("form.update") : t("form.create")}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
