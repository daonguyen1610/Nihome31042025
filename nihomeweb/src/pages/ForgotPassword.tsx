import { useState, useEffect } from "react";
import { Link, useNavigate } from "react-router-dom";
import { Phone, Lock, ArrowRight, KeyRound } from "lucide-react";
import Layout from "@/components/layout/Layout";
import OtpResendButton from "@/components/auth/OtpResendButton";
import { useToast } from "@/hooks/use-toast";
import { useI18n, translateError } from "@/lib/i18n";
import { useAppDispatch, useAppSelector } from "@/store";
import {
  forgotStartThunk,
  forgotVerifyOtpThunk,
  forgotCompleteThunk,
  forgotResetDirectThunk,
  resendForgotOtpThunk,
  clearError,
  clearOtpFlow,
} from "@/store/authSlice";

type Step = "phone" | "otp" | "newPassword" | "done";

const ForgotPassword = () => {
  const navigate = useNavigate();
  const dispatch = useAppDispatch();
  const { toast } = useToast();
  const { t } = useI18n();
  const { loading, error, otpRequired, otpEmail, otpPhone, otpFlow } = useAppSelector((s) => s.auth);

  const [phone, setPhone] = useState("");
  const [otpCode, setOtpCode] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [step, setStep] = useState<Step>("phone");

  useEffect(() => {
    if (error) {
      toast({ title: t("auth.error"), description: translateError(t, error), variant: "destructive" });
      dispatch(clearError());
    }
  }, [error, toast, t, dispatch]);

  useEffect(() => {
    if (otpRequired && otpFlow === "forgot" && step === "phone") {
      setStep("otp");
    }
  }, [otpRequired, otpFlow, step]);

  useEffect(() => {
    return () => {
      dispatch(clearOtpFlow());
    };
  }, [dispatch]);

  const submitPhone = (e: React.FormEvent) => {
    e.preventDefault();
    dispatch(forgotStartThunk(phone)).then((res) => {
      if (res.meta.requestStatus === "fulfilled") {
        const payload = res.payload as { data: { otpRequired: boolean }; phone: string };
        if (!payload.data.otpRequired) {
          // OTP disabled → go straight to new password (direct reset)
          setStep("newPassword");
        }
      }
    });
  };

  const submitOtp = (e: React.FormEvent) => {
    e.preventDefault();
    const p = otpPhone ?? phone;
    dispatch(forgotVerifyOtpThunk({ phone: p, otpCode })).then((res) => {
      if (res.meta.requestStatus === "fulfilled") {
        setStep("newPassword");
      }
    });
  };

  const submitNewPassword = (e: React.FormEvent) => {
    e.preventDefault();
    const p = otpPhone ?? phone;
    const thunk = otpRequired ? forgotCompleteThunk({ phone: p, newPassword }) : forgotResetDirectThunk({ phone: p, newPassword });
    dispatch(thunk).then((res) => {
      if (res.meta.requestStatus === "fulfilled") {
        setStep("done");
        toast({ title: t("auth.forgot.success") });
      }
    });
  };

  const handleResend = async () => {
    const p = otpPhone ?? phone;
    const res = await dispatch(resendForgotOtpThunk(p));
    if (res.meta.requestStatus === "fulfilled") {
      toast({ title: t("auth.otp.resent") });
      return true;
    }
    return false;
  };

  const cardClass = "max-w-md mx-auto bg-card border border-border rounded-3xl p-8 lg:p-10 shadow-elegant";
  const inputClass =
    "w-full bg-secondary rounded-full pl-12 pr-5 py-3.5 text-sm border border-transparent focus:border-primary focus:bg-background outline-none transition";

  return (
    <Layout>
      <section className="min-h-screen pt-32 pb-20 bg-gradient-soft relative overflow-hidden">
        <div className="absolute -top-40 -right-40 w-[500px] h-[500px] bg-primary/15 rounded-full blur-3xl" />
        <div className="absolute -bottom-40 -left-40 w-[500px] h-[500px] bg-accent-orange/15 rounded-full blur-3xl" />

        <div className="container-custom relative">
          {step === "done" ? (
            <div className={cardClass}>
              <h1 className="font-display text-3xl font-extrabold text-center mb-3 tracking-tight">{t("auth.forgot.doneTitle")}</h1>
              <p className="text-center text-muted-foreground text-sm mb-8">{t("auth.forgot.doneDesc")}</p>
              <button
                onClick={() => navigate("/login")}
                className="btn-pill btn-gradient text-white w-full py-3.5 text-sm uppercase tracking-wider"
              >
                {t("auth.login.btn")} <ArrowRight className="w-4 h-4" />
              </button>
            </div>
          ) : step === "newPassword" ? (
            <div className={cardClass}>
              <p className="eyebrow text-primary mb-5 justify-center">{t("auth.forgot.eyebrow")}</p>
              <h1 className="font-display text-3xl font-extrabold text-center mb-3 tracking-tight">{t("auth.forgot.newPwdTitle")}</h1>
              <form onSubmit={submitNewPassword} className="space-y-4">
                <div className="relative">
                  <Lock className="w-4 h-4 absolute left-5 top-1/2 -translate-y-1/2 text-muted-foreground" />
                  <input
                    required
                    type="password"
                    value={newPassword}
                    onChange={(e) => setNewPassword(e.target.value)}
                    placeholder={t("auth.forgot.newPwd")}
                    minLength={6}
                    className={inputClass}
                  />
                </div>
                <button
                  type="submit"
                  disabled={loading}
                  className="btn-pill btn-gradient text-white w-full py-3.5 text-sm uppercase tracking-wider disabled:opacity-60"
                >
                  {loading ? t("auth.processing") : <>{t("auth.forgot.resetBtn")} <ArrowRight className="w-4 h-4" /></>}
                </button>
              </form>
            </div>
          ) : step === "otp" ? (
            <div className={cardClass}>
              <p className="eyebrow text-primary mb-5 justify-center">{t("auth.otp.eyebrow")}</p>
              <h1 className="font-display text-3xl font-extrabold text-center mb-3 tracking-tight">{t("auth.otp.title")}</h1>
              <p className="text-center text-muted-foreground text-sm mb-8">
                {t("auth.otp.desc")} <span className="font-bold">{otpEmail ?? phone}</span>
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
                    className={`${inputClass} text-center tracking-[0.3em] font-mono text-lg`}
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
              <OtpResendButton
                countdownEnabled={step === "otp"}
                loading={loading}
                onResend={handleResend}
              />
            </div>
          ) : (
            <div className={cardClass}>
              <p className="eyebrow text-primary mb-5 justify-center">{t("auth.forgot.eyebrow")}</p>
              <h1 className="font-display text-3xl md:text-4xl font-extrabold text-center mb-3 tracking-tight">
                {t("auth.forgot.title")}
              </h1>
              <p className="text-center text-muted-foreground text-sm mb-8">{t("auth.forgot.desc")}</p>
              <form onSubmit={submitPhone} className="space-y-4">
                <div className="relative">
                  <Phone className="w-4 h-4 absolute left-5 top-1/2 -translate-y-1/2 text-muted-foreground" />
                  <input
                    required
                    type="tel"
                    value={phone}
                    onChange={(e) => setPhone(e.target.value)}
                    placeholder={t("auth.phone")}
                    className={inputClass}
                  />
                </div>
                <button
                  type="submit"
                  disabled={loading}
                  className="btn-pill btn-gradient text-white w-full py-3.5 text-sm uppercase tracking-wider disabled:opacity-60"
                >
                  {loading ? t("auth.processing") : <>{t("auth.forgot.btn")} <ArrowRight className="w-4 h-4" /></>}
                </button>
              </form>
              <div className="text-center mt-6 pt-6 border-t border-border">
                <Link to="/login" className="text-sm font-bold text-primary link-underline">
                  {t("auth.forgot.backLogin")}
                </Link>
              </div>
            </div>
          )}
        </div>
      </section>
    </Layout>
  );
};

export default ForgotPassword;
