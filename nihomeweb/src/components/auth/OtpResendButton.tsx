import { RotateCw } from "lucide-react";
import { useI18n } from "@/lib/i18n";
import { useOtpResendCountdown } from "@/hooks/useOtpResendCountdown";

type OtpResendButtonProps = {
  countdownEnabled: boolean;
  loading: boolean;
  onResend: () => Promise<boolean>;
};

const OtpResendButton = ({
  countdownEnabled,
  loading,
  onResend,
}: OtpResendButtonProps) => {
  const { t } = useI18n();
  const { canResend, formattedTime, restart } = useOtpResendCountdown(countdownEnabled);

  const handleClick = async () => {
    if (!canResend || loading) return;

    try {
      const resent = await onResend();
      if (resent) {
        restart();
      }
    } catch {
      // Treat resend errors as a non-resent outcome; parent code can handle user-facing errors.
    }
  };

  const label = canResend
    ? t("auth.otp.resend")
    : t("auth.otp.resendIn").replace("{{time}}", formattedTime);

  return (
    <button
      type="button"
      onClick={handleClick}
      disabled={loading || !canResend}
      className="flex items-center gap-2 mx-auto mt-4 text-sm text-primary hover:underline disabled:opacity-50 disabled:cursor-not-allowed"
    >
      <RotateCw className="w-3.5 h-3.5" /> {label}
    </button>
  );
};

export default OtpResendButton;
