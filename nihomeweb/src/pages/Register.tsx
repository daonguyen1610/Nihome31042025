import { useState, useEffect } from "react";
import { Link, useNavigate } from "react-router-dom";
import { Phone, Mail, Lock, User, ArrowRight, KeyRound, RotateCw } from "lucide-react";
import Layout from "@/components/layout/Layout";
import { useToast } from "@/hooks/use-toast";
import { useI18n } from "@/lib/i18n";
import { useAppDispatch, useAppSelector } from "@/store";
import {
  registerStartThunk,
  registerVerifyOtpThunk,
  registerCompleteThunk,
  resendRegisterOtpThunk,
  clearError,
  clearOtpFlow,
} from "@/store/authSlice";

const Register = () => {
  const navigate = useNavigate();
  const dispatch = useAppDispatch();
  const { toast } = useToast();
  const { t } = useI18n();
  const { user, loading, error, otpRequired, otpPhone, otpFlow, otpPassword } = useAppSelector((s) => s.auth);

  const [form, setForm] = useState({ fullName: "", phone: "", email: "", password: "" });
  const [otpCode, setOtpCode] = useState("");
  const [otpVerified, setOtpVerified] = useState(false);

  // Navigate on successful registration (user set in store)
  useEffect(() => {
    if (user) {
      toast({ title: t("auth.reg.toast.title"), description: `${t("auth.login.toast.hello")} ${user.fullName}` });
      navigate("/profile");
    }
  }, [user, navigate, toast, t]);

  useEffect(() => {
    if (error) {
      toast({ title: t("auth.error"), description: error, variant: "destructive" });
      dispatch(clearError());
    }
  }, [error, toast, t, dispatch]);

  // Cleanup OTP state on unmount
  useEffect(() => {
    return () => {
      dispatch(clearOtpFlow());
    };
  }, [dispatch]);

  const submitRegister = (e: React.FormEvent) => {
    e.preventDefault();
    dispatch(registerStartThunk({ phone: form.phone, fullName: form.fullName, email: form.email, password: form.password }));
  };

  const submitOtp = (e: React.FormEvent) => {
    e.preventDefault();
    if (!otpPhone) return;
    dispatch(registerVerifyOtpThunk({ phone: otpPhone, otpCode })).then((res) => {
      if (res.meta.requestStatus === "fulfilled") {
        setOtpVerified(true);
        // Immediately complete registration
        dispatch(registerCompleteThunk({ phone: otpPhone, password: otpPassword ?? form.password }));
      }
    });
  };

  const handleResend = () => {
    if (!otpPhone) return;
    dispatch(resendRegisterOtpThunk(otpPhone)).then((res) => {
      if (res.meta.requestStatus === "fulfilled") {
        toast({ title: t("auth.otp.resent") });
      }
    });
  };

  // Show OTP form if required
  if (otpRequired && otpFlow === "register" && !otpVerified) {
    return (
      <Layout>
        <section className="min-h-screen pt-32 pb-20 bg-gradient-soft relative overflow-hidden">
          <div className="absolute -top-40 -right-40 w-[500px] h-[500px] bg-accent-orange/15 rounded-full blur-3xl" />
          <div className="absolute -bottom-40 -left-40 w-[500px] h-[500px] bg-primary/15 rounded-full blur-3xl" />
          <div className="container-custom relative">
            <div className="max-w-md mx-auto bg-card border border-border rounded-3xl p-8 lg:p-10 shadow-elegant">
              <p className="eyebrow text-primary mb-5 justify-center">{t("auth.otp.eyebrow")}</p>
              <h1 className="font-display text-3xl md:text-4xl font-extrabold text-center mb-3 tracking-tight">
                {t("auth.otp.title")}
              </h1>
              <p className="text-center text-muted-foreground text-sm mb-8">
                {t("auth.otp.desc")} <span className="font-bold">{otpPhone}</span>
              </p>

              <form onSubmit={submitOtp} className="space-y-4">
                <div className="relative">
                  <KeyRound className="w-4 h-4 absolute left-5 top-1/2 -translate-y-1/2 text-muted-foreground" />
                  <input
                    required
                    value={otpCode}
                    onChange={(e) => setOtpCode(e.target.value)}
                    placeholder={t("auth.otp.placeholder")}
                    maxLength={6}
                    className="w-full bg-secondary rounded-full pl-12 pr-5 py-3.5 text-sm border border-transparent focus:border-primary focus:bg-background outline-none transition text-center tracking-[0.3em] font-mono text-lg"
                  />
                </div>
                <button
                  type="submit"
                  disabled={loading}
                  className="btn-pill btn-gradient text-white w-full py-3.5 text-sm uppercase tracking-wider disabled:opacity-60"
                >
                  {loading ? t("auth.processing") : <>{t("auth.otp.verify")} <ArrowRight className="w-4 h-4" /></>}
                </button>
              </form>

              <button
                onClick={handleResend}
                disabled={loading}
                className="flex items-center gap-2 mx-auto mt-4 text-sm text-primary hover:underline disabled:opacity-50"
              >
                <RotateCw className="w-3.5 h-3.5" /> {t("auth.otp.resend")}
              </button>
            </div>
          </div>
        </section>
      </Layout>
    );
  }

  return (
    <Layout>
      <section className="min-h-screen pt-32 pb-20 bg-gradient-soft relative overflow-hidden">
        <div className="absolute -top-40 -right-40 w-[500px] h-[500px] bg-accent-orange/15 rounded-full blur-3xl" />
        <div className="absolute -bottom-40 -left-40 w-[500px] h-[500px] bg-primary/15 rounded-full blur-3xl" />

        <div className="container-custom relative">
          <div className="max-w-md mx-auto bg-card border border-border rounded-3xl p-8 lg:p-10 shadow-elegant">
            <p className="eyebrow text-primary mb-5 justify-center">{t("auth.reg.eyebrow")}</p>
            <h1 className="font-display text-3xl md:text-4xl font-extrabold text-center mb-3 tracking-tight">
              {t("auth.reg.titleA")} <span className="text-gradient-primary">{t("auth.reg.titleB")}</span>
            </h1>
            <p className="text-center text-muted-foreground text-sm mb-8">{t("auth.reg.desc")}</p>

            <form onSubmit={submitRegister} className="space-y-4">
              <div className="relative">
                <User className="w-4 h-4 absolute left-5 top-1/2 -translate-y-1/2 text-muted-foreground" />
                <input
                  required
                  value={form.fullName}
                  onChange={(e) => setForm({ ...form, fullName: e.target.value })}
                  placeholder={t("auth.reg.fullName")}
                  className="w-full bg-secondary rounded-full pl-12 pr-5 py-3.5 text-sm border border-transparent focus:border-primary focus:bg-background outline-none transition"
                />
              </div>
              <div className="relative">
                <Phone className="w-4 h-4 absolute left-5 top-1/2 -translate-y-1/2 text-muted-foreground" />
                <input
                  required
                  type="tel"
                  value={form.phone}
                  onChange={(e) => setForm({ ...form, phone: e.target.value })}
                  placeholder={t("auth.phone")}
                  className="w-full bg-secondary rounded-full pl-12 pr-5 py-3.5 text-sm border border-transparent focus:border-primary focus:bg-background outline-none transition"
                />
              </div>
              <div className="relative">
                <Mail className="w-4 h-4 absolute left-5 top-1/2 -translate-y-1/2 text-muted-foreground" />
                <input
                  required
                  type="email"
                  value={form.email}
                  onChange={(e) => setForm({ ...form, email: e.target.value })}
                  placeholder={t("auth.email")}
                  className="w-full bg-secondary rounded-full pl-12 pr-5 py-3.5 text-sm border border-transparent focus:border-primary focus:bg-background outline-none transition"
                />
              </div>
              <div className="relative">
                <Lock className="w-4 h-4 absolute left-5 top-1/2 -translate-y-1/2 text-muted-foreground" />
                <input
                  required
                  type="password"
                  value={form.password}
                  onChange={(e) => setForm({ ...form, password: e.target.value })}
                  placeholder={t("auth.password")}
                  minLength={6}
                  className="w-full bg-secondary rounded-full pl-12 pr-5 py-3.5 text-sm border border-transparent focus:border-primary focus:bg-background outline-none transition"
                />
              </div>
              <button
                type="submit"
                disabled={loading}
                className="btn-pill btn-gradient text-white w-full py-3.5 text-sm uppercase tracking-wider disabled:opacity-60"
              >
                {loading ? t("auth.processing") : <>{t("auth.reg.btn")} <ArrowRight className="w-4 h-4" /></>}
              </button>
            </form>

            <div className="text-center mt-6 pt-6 border-t border-border">
              <p className="text-sm text-muted-foreground">
                {t("auth.reg.hasAcc")}{" "}
                <Link to="/login" className="font-bold text-primary link-underline">
                  {t("auth.login.btn")}
                </Link>
              </p>
            </div>
          </div>
        </div>
      </section>
    </Layout>
  );
};

export default Register;
