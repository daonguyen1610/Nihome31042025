import { useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { useI18n } from "@/lib/i18n";
import { logoutThunk } from "@/store/authSlice";
import { useAppDispatch } from "@/store";

const Forbidden = () => {
  const { t } = useI18n();
  const dispatch = useAppDispatch();
  const navigate = useNavigate();
  const [loggingOut, setLoggingOut] = useState(false);

  const switchAccount = async () => {
    setLoggingOut(true);
    await dispatch(logoutThunk());
    navigate("/login", { replace: true });
  };

  return (
    <div className="flex min-h-screen items-center justify-center bg-muted px-6">
      <div className="max-w-md text-center">
        <h1 className="mb-4 text-4xl font-bold">403</h1>
        <p className="mb-2 text-xl font-semibold text-foreground">{t("forbidden.title")}</p>
        <p className="mb-6 text-sm text-muted-foreground">{t("forbidden.recovery")}</p>
        <div className="flex flex-wrap justify-center gap-3">
          <button
            type="button"
            onClick={() => void switchAccount()}
            disabled={loggingOut}
            className="inline-flex min-h-11 items-center justify-center rounded-md bg-primary px-4 py-2 text-sm font-medium text-primary-foreground hover:bg-primary/90 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 disabled:cursor-wait disabled:opacity-70"
          >
            {t(loggingOut ? "forbidden.switchingAccount" : "forbidden.switchAccount")}
          </button>
          <Link
            to="/my-profile"
            className="inline-flex min-h-11 items-center justify-center rounded-md border border-border bg-card px-4 py-2 text-sm font-medium text-foreground hover:bg-muted focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2"
          >
            {t("forbidden.profile")}
          </Link>
        </div>
      </div>
    </div>
  );
};

export default Forbidden;
