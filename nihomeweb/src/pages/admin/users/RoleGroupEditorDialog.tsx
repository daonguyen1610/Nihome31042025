import { useEffect, useState } from "react";
import { useMutation } from "@tanstack/react-query";
import { Link } from "react-router-dom";
import { isAxiosError } from "axios";
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
import { useToast } from "@/hooks/use-toast";
import { usePermissions } from "@/hooks/usePermissions";
import { TRANSLATIONS_CHANGED_EVENT, useI18n } from "@/lib/i18n";
import { rbacApi, type RoleGroupNames, type RoleGroupResponse } from "@/services/rbacApi";

const CODE_RE = /^[A-Z][A-Z0-9_]{1,49}$/;
const EMPTY_NAMES: RoleGroupNames = { vi: "" };

interface Props {
  open: boolean;
  group: RoleGroupResponse | null;
  onOpenChange: (open: boolean) => void;
  onSaved: (group: RoleGroupResponse) => Promise<void>;
}

export default function RoleGroupEditorDialog({ open, group, onOpenChange, onSaved }: Props) {
  const { t } = useI18n();
  const { toast } = useToast();
  const { has } = usePermissions();
  const [code, setCode] = useState("");
  const [names, setNames] = useState<RoleGroupNames>(EMPTY_NAMES);
  const [sortOrder, setSortOrder] = useState("0");

  useEffect(() => {
    if (!open) return;
    setCode(group?.code ?? "");
    setNames({
      vi: group?.names?.vi ?? "",
    });
    setSortOrder((group?.sortOrder ?? 0).toLocaleString("vi-VN"));
  }, [open, group]);

  const parsedOrder = Number(sortOrder.replace(/\./g, ""));
  const valid =
    CODE_RE.test(code) &&
    names.vi.trim().length >= 2 &&
    names.vi.trim().length <= 100 &&
    sortOrder.trim().length > 0 &&
    Number.isInteger(parsedOrder) &&
    parsedOrder >= 0 &&
    parsedOrder <= 2147483647;

  const save = useMutation({
    mutationFn: async () => {
      const body = { names, sortOrder: parsedOrder };
      return group
        ? (await rbacApi.updateRoleGroup(group.id, body)).data
        : (await rbacApi.createRoleGroup({ ...body, code })).data;
    },
    onSuccess: async (saved) => {
      window.dispatchEvent(new Event(TRANSLATIONS_CHANGED_EVENT));
      await onSaved(saved);
      onOpenChange(false);
      toast({ title: t(group ? "adminRbac.toast.groupSaved" : "adminRbac.groups.created") });
    },
    onError: (error) => {
      const message = isAxiosError(error) ? error.response?.data?.message : null;
      toast({
        title: t("adminRbac.toast.groupSaveFailed"),
        description: typeof message === "string" ? message : undefined,
        variant: "destructive",
      });
    },
  });

  return (
    <Dialog open={open} onOpenChange={(next) => !save.isPending && onOpenChange(next)}>
      <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>
            {t(group ? "adminRbac.groups.edit" : "adminRbac.groups.create")}
          </DialogTitle>
        </DialogHeader>
        <form
          className="space-y-4"
          onSubmit={(event) => {
            event.preventDefault();
            if (valid) save.mutate();
          }}
        >
          <div className="space-y-1.5">
            <Label htmlFor="role-group-code">{t("adminRbac.groups.codeLabel")}</Label>
            <Input
              id="role-group-code"
              value={code}
              onChange={(event) => setCode(event.target.value.toUpperCase())}
              disabled={group != null}
              maxLength={50}
              aria-invalid={code.length > 0 && !CODE_RE.test(code)}
              data-testid="role-group-code"
            />
            <p className="text-xs text-muted-foreground">{t("adminRbac.groups.codeHelp")}</p>
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="role-group-name-vi">{t("adminRbac.groups.language.vi")}</Label>
            <Input
              id="role-group-name-vi"
              value={names.vi}
              onChange={(event) => setNames({ vi: event.target.value })}
              minLength={2}
              maxLength={100}
              required
              aria-invalid={names.vi.length > 0 && names.vi.trim().length < 2}
              data-testid="role-group-name-vi"
            />
            <p className="text-xs text-muted-foreground">
              {has("content.translations.view") ? (
                <Link
                  to="/admin/translations?tab=entity&type=RoleGroup"
                  className="underline underline-offset-2"
                >
                  {t("adminRbac.groups.translationHelp")}
                </Link>
              ) : (
                t("adminRbac.groups.translationHelpNoAccess")
              )}
            </p>
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="role-group-order">{t("adminRbac.groups.sortOrder")}</Label>
            <Input
              id="role-group-order"
              inputMode="numeric"
              value={sortOrder}
              onChange={(event) => {
                const digits = event.target.value.replace(/\D/g, "");
                setSortOrder(digits ? Number(digits).toLocaleString("vi-VN") : "");
              }}
              aria-invalid={
                sortOrder.trim().length === 0 ||
                !Number.isInteger(parsedOrder) ||
                parsedOrder < 0 ||
                parsedOrder > 2147483647
              }
              data-testid="role-group-order"
            />
          </div>
          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>
              {t("common.cancel")}
            </Button>
            <Button type="submit" disabled={!valid || save.isPending} data-testid="role-group-save">
              {t("common.save")}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
