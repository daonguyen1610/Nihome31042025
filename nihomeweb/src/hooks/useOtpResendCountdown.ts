import { useCallback, useEffect, useMemo, useRef, useState } from "react";

export const OTP_RESEND_COOLDOWN_SECONDS = 5 * 60;

const formatRemainingTime = (seconds: number) => {
  const minutes = Math.floor(seconds / 60).toString().padStart(2, "0");
  const restSeconds = (seconds % 60).toString().padStart(2, "0");
  return `${minutes}:${restSeconds}`;
};

export const useOtpResendCountdown = (
  enabled: boolean,
  initialSeconds = OTP_RESEND_COOLDOWN_SECONDS,
) => {
  const [remainingSeconds, setRemainingSeconds] = useState(() =>
    enabled ? initialSeconds : 0,
  );
  const wasEnabledRef = useRef(enabled);

  useEffect(() => {
    if (enabled && !wasEnabledRef.current) {
      setRemainingSeconds(initialSeconds);
    }

    if (!enabled) {
      setRemainingSeconds(0);
    }

    wasEnabledRef.current = enabled;
  }, [enabled, initialSeconds]);

  useEffect(() => {
    if (!enabled) return;

    const timer = window.setInterval(() => {
      setRemainingSeconds((current) => {
        if (current <= 1) {
          window.clearInterval(timer);
          return 0;
        }

        return current - 1;
      });
    }, 1000);

    return () => window.clearInterval(timer);
  }, [enabled, initialSeconds]);

  const restart = useCallback(() => {
    setRemainingSeconds(initialSeconds);
  }, [initialSeconds]);

  const formattedTime = useMemo(
    () => formatRemainingTime(remainingSeconds),
    [remainingSeconds],
  );

  return {
    canResend: remainingSeconds === 0,
    formattedTime,
    remainingSeconds,
    restart,
  };
};
