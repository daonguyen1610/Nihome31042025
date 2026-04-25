import { createSlice, createAsyncThunk, type PayloadAction } from "@reduxjs/toolkit";
import { authApi, type AuthResponse } from "@/services/authApi";
import { isAxiosError } from "axios";

// --- State types ---

export interface AuthUser {
  userId: number;
  phoneNumber: string;
  fullName: string;
  email?: string;
  role: string;
  isActive: boolean;
  avatarUrl?: string;
}

interface AuthState {
  user: AuthUser | null;
  accessToken: string | null;
  refreshToken: string | null;
  loading: boolean;
  error: string | null;
  // OTP flow state
  otpRequired: boolean;
  otpPhone: string | null;
  otpFlow: "register" | "forgot" | null;
  otpPassword: string | null; // kept in memory for register-complete
}

// --- Helpers ---

const ACCESS_TOKEN_KEY = "nicon_access_token";
const REFRESH_TOKEN_KEY = "nicon_refresh_token";

function setCookie(name: string, value: string, days = 7) {
  const expires = new Date(Date.now() + days * 864e5).toUTCString();
  const secure = location.protocol === "https:" ? "; Secure" : "";
  document.cookie = `${name}=${encodeURIComponent(value)}; expires=${expires}; path=/; SameSite=Strict${secure}`;
}

function getCookie(name: string): string | null {
  const match = document.cookie.match(new RegExp(`(?:^|; )${name}=([^;]*)`));
  return match ? decodeURIComponent(match[1]) : null;
}

function deleteCookie(name: string) {
  const secure = location.protocol === "https:" ? "; Secure" : "";
  document.cookie = `${name}=; expires=Thu, 01 Jan 1970 00:00:00 GMT; path=/; SameSite=Strict${secure}`;
}

function persistTokens(accessToken: string, refreshToken: string) {
  setCookie(ACCESS_TOKEN_KEY, accessToken);
  setCookie(REFRESH_TOKEN_KEY, refreshToken);
}

function loadTokens(): { accessToken: string; refreshToken: string } | null {
  const accessToken = getCookie(ACCESS_TOKEN_KEY);
  const refreshToken = getCookie(REFRESH_TOKEN_KEY);
  if (accessToken && refreshToken) return { accessToken, refreshToken };
  return null;
}

function clearTokens() {
  deleteCookie(ACCESS_TOKEN_KEY);
  deleteCookie(REFRESH_TOKEN_KEY);
}

function userFromAuth(r: AuthResponse): AuthUser {
  return {
    userId: r.userId,
    phoneNumber: r.phoneNumber,
    fullName: r.fullName,
    email: r.email,
    role: r.role,
    isActive: r.isActive,
    avatarUrl: r.avatarUrl,
  };
}

function extractError(err: unknown): string {
  if (isAxiosError(err)) {
    const data = err.response?.data;
    if (typeof data === "string") return data;
    if (data?.message) return data.message;
    if (data?.title) return data.title;
    if (data?.errors) {
      const msgs = Object.values(data.errors).flat();
      return msgs.join(". ");
    }
    return err.message;
  }
  return "An unexpected error occurred";
}

// --- Thunks ---

export const loginThunk = createAsyncThunk("auth/login", async (payload: { phone: string; password: string }, { rejectWithValue }) => {
  try {
    const { data } = await authApi.login(payload.phone, payload.password);
    persistTokens(data.accessToken, data.refreshToken);
    return data;
  } catch (err) {
    return rejectWithValue(extractError(err));
  }
});

export const registerStartThunk = createAsyncThunk(
  "auth/registerStart",
  async (payload: { phone: string; fullName: string; email: string; password: string }, { rejectWithValue }) => {
    try {
      const { data } = await authApi.registerStart(payload.phone, payload.fullName, payload.email, payload.password);
      return { data, password: payload.password, phone: payload.phone };
    } catch (err) {
      return rejectWithValue(extractError(err));
    }
  },
);

export const registerVerifyOtpThunk = createAsyncThunk(
  "auth/registerVerifyOtp",
  async (payload: { phone: string; otpCode: string }, { rejectWithValue }) => {
    try {
      const { data } = await authApi.registerVerifyOtp(payload.phone, payload.otpCode);
      return data;
    } catch (err) {
      return rejectWithValue(extractError(err));
    }
  },
);

export const registerCompleteThunk = createAsyncThunk(
  "auth/registerComplete",
  async (payload: { phone: string; password: string }, { rejectWithValue }) => {
    try {
      const { data } = await authApi.registerComplete(payload.phone, payload.password);
      persistTokens(data.accessToken, data.refreshToken);
      return data;
    } catch (err) {
      return rejectWithValue(extractError(err));
    }
  },
);

export const resendRegisterOtpThunk = createAsyncThunk("auth/resendRegisterOtp", async (phone: string, { rejectWithValue }) => {
  try {
    const { data } = await authApi.registerResendOtp(phone);
    return data;
  } catch (err) {
    return rejectWithValue(extractError(err));
  }
});

export const forgotStartThunk = createAsyncThunk("auth/forgotStart", async (phone: string, { rejectWithValue }) => {
  try {
    const { data } = await authApi.forgotStart(phone);
    return { data, phone };
  } catch (err) {
    return rejectWithValue(extractError(err));
  }
});

export const forgotVerifyOtpThunk = createAsyncThunk(
  "auth/forgotVerifyOtp",
  async (payload: { phone: string; otpCode: string }, { rejectWithValue }) => {
    try {
      const { data } = await authApi.forgotVerifyOtp(payload.phone, payload.otpCode);
      return data;
    } catch (err) {
      return rejectWithValue(extractError(err));
    }
  },
);

export const forgotCompleteThunk = createAsyncThunk(
  "auth/forgotComplete",
  async (payload: { phone: string; newPassword: string }, { rejectWithValue }) => {
    try {
      const { data } = await authApi.forgotComplete(payload.phone, payload.newPassword);
      return data;
    } catch (err) {
      return rejectWithValue(extractError(err));
    }
  },
);

export const forgotResetDirectThunk = createAsyncThunk(
  "auth/forgotResetDirect",
  async (payload: { phone: string; newPassword: string }, { rejectWithValue }) => {
    try {
      const { data } = await authApi.forgotResetDirect(payload.phone, payload.newPassword);
      return data;
    } catch (err) {
      return rejectWithValue(extractError(err));
    }
  },
);

export const resendForgotOtpThunk = createAsyncThunk("auth/resendForgotOtp", async (phone: string, { rejectWithValue }) => {
  try {
    const { data } = await authApi.forgotResendOtp(phone);
    return data;
  } catch (err) {
    return rejectWithValue(extractError(err));
  }
});

export const refreshThunk = createAsyncThunk("auth/refresh", async (_, { getState, rejectWithValue }) => {
  const state = getState() as { auth: AuthState };
  const token = state.auth.refreshToken;
  if (!token) return rejectWithValue("No refresh token");
  try {
    const { data } = await authApi.refresh(token);
    persistTokens(data.accessToken, data.refreshToken);
    return data;
  } catch (err) {
    clearTokens();
    return rejectWithValue(extractError(err));
  }
});

export const logoutThunk = createAsyncThunk("auth/logout", async (_, { getState }) => {
  const state = getState() as { auth: AuthState };
  const token = state.auth.refreshToken;
  if (token) {
    try {
      await authApi.logout(token);
    } catch {
      // ignore logout errors
    }
  }
  clearTokens();
});

// --- Initial state ---

const saved = loadTokens();

const initialState: AuthState = {
  user: null,
  accessToken: saved?.accessToken ?? null,
  refreshToken: saved?.refreshToken ?? null,
  loading: false,
  error: null,
  otpRequired: false,
  otpPhone: null,
  otpFlow: null,
  otpPassword: null,
};

// --- Slice ---

const authSlice = createSlice({
  name: "auth",
  initialState,
  reducers: {
    clearError(state) {
      state.error = null;
    },
    clearOtpFlow(state) {
      state.otpRequired = false;
      state.otpPhone = null;
      state.otpFlow = null;
      state.otpPassword = null;
    },
    setUser(state, action: PayloadAction<AuthUser | null>) {
      state.user = action.payload;
    },
  },
  extraReducers: (builder) => {
    // Login
    builder.addCase(loginThunk.pending, (state) => {
      state.loading = true;
      state.error = null;
    });
    builder.addCase(loginThunk.fulfilled, (state, { payload }) => {
      state.loading = false;
      state.user = userFromAuth(payload);
      state.accessToken = payload.accessToken;
      state.refreshToken = payload.refreshToken;
    });
    builder.addCase(loginThunk.rejected, (state, { payload }) => {
      state.loading = false;
      state.error = payload as string;
    });

    // Register start
    builder.addCase(registerStartThunk.pending, (state) => {
      state.loading = true;
      state.error = null;
    });
    builder.addCase(registerStartThunk.fulfilled, (state, { payload }) => {
      state.loading = false;
      const d = payload.data;
      if ("accessToken" in d) {
        // OTP disabled → auto-completed registration
        const auth = d as AuthResponse;
        state.user = userFromAuth(auth);
        state.accessToken = auth.accessToken;
        state.refreshToken = auth.refreshToken;
        persistTokens(auth.accessToken, auth.refreshToken);
      } else {
        // OTP required
        state.otpRequired = true;
        state.otpPhone = payload.phone;
        state.otpFlow = "register";
        state.otpPassword = payload.password;
      }
    });
    builder.addCase(registerStartThunk.rejected, (state, { payload }) => {
      state.loading = false;
      state.error = payload as string;
    });

    // Register verify OTP
    builder.addCase(registerVerifyOtpThunk.pending, (state) => {
      state.loading = true;
      state.error = null;
    });
    builder.addCase(registerVerifyOtpThunk.fulfilled, (state) => {
      state.loading = false;
    });
    builder.addCase(registerVerifyOtpThunk.rejected, (state, { payload }) => {
      state.loading = false;
      state.error = payload as string;
    });

    // Register complete
    builder.addCase(registerCompleteThunk.pending, (state) => {
      state.loading = true;
      state.error = null;
    });
    builder.addCase(registerCompleteThunk.fulfilled, (state, { payload }) => {
      state.loading = false;
      state.user = userFromAuth(payload);
      state.accessToken = payload.accessToken;
      state.refreshToken = payload.refreshToken;
      state.otpRequired = false;
      state.otpPhone = null;
      state.otpFlow = null;
      state.otpPassword = null;
    });
    builder.addCase(registerCompleteThunk.rejected, (state, { payload }) => {
      state.loading = false;
      state.error = payload as string;
    });

    // Resend register OTP
    builder.addCase(resendRegisterOtpThunk.rejected, (state, { payload }) => {
      state.error = payload as string;
    });

    // Forgot start
    builder.addCase(forgotStartThunk.pending, (state) => {
      state.loading = true;
      state.error = null;
    });
    builder.addCase(forgotStartThunk.fulfilled, (state, { payload }) => {
      state.loading = false;
      if (payload.data.otpRequired) {
        state.otpRequired = true;
        state.otpPhone = payload.phone;
        state.otpFlow = "forgot";
      }
    });
    builder.addCase(forgotStartThunk.rejected, (state, { payload }) => {
      state.loading = false;
      state.error = payload as string;
    });

    // Forgot verify OTP
    builder.addCase(forgotVerifyOtpThunk.pending, (state) => {
      state.loading = true;
      state.error = null;
    });
    builder.addCase(forgotVerifyOtpThunk.fulfilled, (state) => {
      state.loading = false;
    });
    builder.addCase(forgotVerifyOtpThunk.rejected, (state, { payload }) => {
      state.loading = false;
      state.error = payload as string;
    });

    // Forgot complete / reset direct
    builder.addCase(forgotCompleteThunk.pending, (state) => {
      state.loading = true;
      state.error = null;
    });
    builder.addCase(forgotCompleteThunk.fulfilled, (state) => {
      state.loading = false;
      state.otpRequired = false;
      state.otpPhone = null;
      state.otpFlow = null;
    });
    builder.addCase(forgotCompleteThunk.rejected, (state, { payload }) => {
      state.loading = false;
      state.error = payload as string;
    });
    builder.addCase(forgotResetDirectThunk.pending, (state) => {
      state.loading = true;
      state.error = null;
    });
    builder.addCase(forgotResetDirectThunk.fulfilled, (state) => {
      state.loading = false;
    });
    builder.addCase(forgotResetDirectThunk.rejected, (state, { payload }) => {
      state.loading = false;
      state.error = payload as string;
    });

    // Resend forgot OTP
    builder.addCase(resendForgotOtpThunk.rejected, (state, { payload }) => {
      state.error = payload as string;
    });

    // Refresh
    builder.addCase(refreshThunk.fulfilled, (state, { payload }) => {
      state.user = userFromAuth(payload);
      state.accessToken = payload.accessToken;
      state.refreshToken = payload.refreshToken;
    });
    builder.addCase(refreshThunk.rejected, (state) => {
      state.user = null;
      state.accessToken = null;
      state.refreshToken = null;
    });

    // Logout
    builder.addCase(logoutThunk.fulfilled, (state) => {
      state.user = null;
      state.accessToken = null;
      state.refreshToken = null;
      state.otpRequired = false;
      state.otpPhone = null;
      state.otpFlow = null;
      state.otpPassword = null;
    });
  },
});

export const { clearError, clearOtpFlow, setUser } = authSlice.actions;
export default authSlice.reducer;
