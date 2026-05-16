import { createAsyncThunk, createSlice } from "@reduxjs/toolkit";
import { isAxiosError } from "axios";
import { notificationApi, type NotificationDto } from "@/services/notificationApi";

interface NotificationState {
  items: NotificationDto[];
  unreadCount: number;
  loading: boolean;
  error: string | null;
}

const initialState: NotificationState = {
  items: [],
  unreadCount: 0,
  loading: false,
  error: null,
};

function extractError(err: unknown): string {
  if (isAxiosError(err)) {
    const data = err.response?.data;
    if (typeof data === "string") return data;
    if (data?.message) return data.message;
    if (data?.title) return data.title;
    return err.message;
  }
  return "An unexpected error occurred";
}

export const fetchNotifications = createAsyncThunk(
  "notifications/fetchAll",
  async (payload: { skip?: number; take?: number } | undefined, { rejectWithValue }) => {
    try {
      const { data } = await notificationApi.getAll(payload?.skip ?? 0, payload?.take ?? 20);
      return data;
    } catch (err) {
      return rejectWithValue(extractError(err));
    }
  },
);

export const fetchUnreadCount = createAsyncThunk(
  "notifications/fetchUnreadCount",
  async (_, { rejectWithValue }) => {
    try {
      const { data } = await notificationApi.getUnreadCount();
      return data.count;
    } catch (err) {
      return rejectWithValue(extractError(err));
    }
  },
);

export const markNotificationRead = createAsyncThunk(
  "notifications/markRead",
  async (id: number, { rejectWithValue }) => {
    try {
      const { data } = await notificationApi.markRead(id);
      return data;
    } catch (err) {
      return rejectWithValue(extractError(err));
    }
  },
);

export const markAllNotificationsRead = createAsyncThunk(
  "notifications/markAllRead",
  async (_, { rejectWithValue }) => {
    try {
      const { data } = await notificationApi.markAllRead();
      return data.count;
    } catch (err) {
      return rejectWithValue(extractError(err));
    }
  },
);

export const removeNotification = createAsyncThunk(
  "notifications/remove",
  async (id: number, { rejectWithValue }) => {
    try {
      await notificationApi.delete(id);
      return id;
    } catch (err) {
      return rejectWithValue(extractError(err));
    }
  },
);

const notificationSlice = createSlice({
  name: "notifications",
  initialState,
  reducers: {
    clearNotificationError(state) {
      state.error = null;
    },
    resetNotifications(state) {
      state.items = [];
      state.unreadCount = 0;
      state.loading = false;
      state.error = null;
    },
  },
  extraReducers: (builder) => {
    builder.addCase(fetchNotifications.pending, (state) => {
      state.loading = true;
      state.error = null;
    });
    builder.addCase(fetchNotifications.fulfilled, (state, { payload }) => {
      state.loading = false;
      state.items = payload;
    });
    builder.addCase(fetchNotifications.rejected, (state, { payload }) => {
      state.loading = false;
      state.error = payload as string;
    });

    builder.addCase(fetchUnreadCount.fulfilled, (state, { payload }) => {
      state.unreadCount = payload;
    });
    builder.addCase(fetchUnreadCount.rejected, (state, { payload }) => {
      state.error = payload as string;
    });

    builder.addCase(markNotificationRead.fulfilled, (state, { payload }) => {
      const item = state.items.find((notification) => notification.id === payload.id);
      if (item && !item.isRead && payload.isRead) {
        state.unreadCount = Math.max(0, state.unreadCount - 1);
      }

      const index = state.items.findIndex((notification) => notification.id === payload.id);
      if (index >= 0) state.items[index] = payload;
    });
    builder.addCase(markNotificationRead.rejected, (state, { payload }) => {
      state.error = payload as string;
    });

    builder.addCase(markAllNotificationsRead.fulfilled, (state) => {
      state.items.forEach((item) => {
        item.isRead = true;
      });
      state.unreadCount = 0;
    });
    builder.addCase(markAllNotificationsRead.rejected, (state, { payload }) => {
      state.error = payload as string;
    });

    builder.addCase(removeNotification.fulfilled, (state, { payload }) => {
      const item = state.items.find((notification) => notification.id === payload);
      if (item && !item.isRead) {
        state.unreadCount = Math.max(0, state.unreadCount - 1);
      }
      state.items = state.items.filter((notification) => notification.id !== payload);
    });
    builder.addCase(removeNotification.rejected, (state, { payload }) => {
      state.error = payload as string;
    });
  },
});

export const { clearNotificationError, resetNotifications } = notificationSlice.actions;

export default notificationSlice.reducer;
