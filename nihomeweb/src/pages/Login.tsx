import { useState, useEffect } from "react";
import { Link, useNavigate } from "react-router-dom";
import { Phone, Lock, ArrowRight } from "lucide-react";
import Layout from "@/components/layout/Layout";
import { useToast } from "@/hooks/use-toast";
import { useI18n, translateError } from "@/lib/i18n";
import { isAdminRole } from "@/lib/auth";
import { useAppDispatch, useAppSelector } from "@/store";
import { loginThunk, clearError } from "@/store/authSlice";

const Login = () => {
  const navigate = useNavigate();
  const dispatch = useAppDispatch();
  const { toast } = useToast();
  const { t } = useI18n();
  const { user, loading, error } = useAppSelector((s) => s.auth);
  const [phone, setPhone] = useState("");
  const [password, setPassword] = useState("");

  useEffect(() => {
    if (user) {
      toast({ title: t("auth.login.toast.title"), description: `${t("auth.login.toast.hello")} ${user.fullName}` });
      navigate(isAdminRole(user.role) ? "/admin" : "/profile");
    }
  }, [user, navigate, toast, t]);

  useEffect(() => {
    if (error) {
      toast({ title: t("auth.error"), description: translateError(t, error), variant: "destructive" });
      dispatch(clearError());
    }
  }, [error, toast, t, dispatch]);

  const submit = (e: React.FormEvent) => {
    e.preventDefault();
    dispatch(loginThunk({ phone, password }));
  };

  return (
    <Layout>
      <section className="min-h-screen pt-32 pb-20 bg-gradient-soft relative overflow-hidden">
        <div className="absolute -top-40 -right-40 w-[500px] h-[500px] bg-primary/15 rounded-full blur-3xl" />
        <div className="absolute -bottom-40 -left-40 w-[500px] h-[500px] bg-accent-orange/15 rounded-full blur-3xl" />

        <div className="container-custom relative">
          <div className="max-w-md mx-auto bg-card border border-border rounded-3xl p-8 lg:p-10 shadow-elegant">
            <p className="eyebrow text-primary mb-5 justify-center">{t("auth.login.eyebrow")}</p>
            <h1 className="font-display text-3xl md:text-4xl font-extrabold text-center mb-3 tracking-tight">
              {t("auth.login.titleA")} <span className="text-gradient-primary">{t("auth.login.titleB")}</span>
            </h1>
            <p className="text-center text-muted-foreground text-sm mb-8">{t("auth.login.desc")}</p>

            <form onSubmit={submit} className="space-y-4">
              <div className="relative">
                <Phone className="w-4 h-4 absolute left-5 top-1/2 -translate-y-1/2 text-muted-foreground" />
                <input
                  required
                  type="tel"
                  value={phone}
                  onChange={(e) => setPhone(e.target.value)}
                  placeholder={t("auth.phone")}
                  className="w-full bg-secondary rounded-full pl-12 pr-5 py-3.5 text-sm border border-transparent focus:border-primary focus:bg-background outline-none transition"
                />
              </div>
              <div className="relative">
                <Lock className="w-4 h-4 absolute left-5 top-1/2 -translate-y-1/2 text-muted-foreground" />
                <input
                  required
                  type="password"
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  placeholder={t("auth.password")}
                  className="w-full bg-secondary rounded-full pl-12 pr-5 py-3.5 text-sm border border-transparent focus:border-primary focus:bg-background outline-none transition"
                />
              </div>
              <button
                type="submit"
                disabled={loading}
                className="btn-pill btn-gradient text-white w-full py-3.5 text-sm uppercase tracking-wider disabled:opacity-60"
              >
                {loading ? t("auth.processing") : <>{t("auth.login.btn")} <ArrowRight className="w-4 h-4" /></>}
              </button>
            </form>

            <div className="text-center mt-4">
              <Link to="/forgot-password" className="text-sm text-primary link-underline">
                {t("auth.login.forgot")}
              </Link>
            </div>

            <div className="text-center mt-6 pt-6 border-t border-border">
              <p className="text-sm text-muted-foreground">
                {t("auth.login.noAcc")}{" "}
                <Link to="/register" className="font-bold text-primary link-underline">
                  {t("auth.login.signup")}
                </Link>
              </p>
            </div>
          </div>
        </div>
      </section>
    </Layout>
  );
};

export default Login;
