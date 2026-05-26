import { createAsyncThunk, createSlice } from "@reduxjs/toolkit";
import { isAxiosError } from "axios";
import { notificationApi, type NotificationDto } from "@/services/notificationApi";

interface NotificationState {
  items: NotificationDto[];
  unreadCount: number;
  loading: boolean;
  countLoading: boolean;
  error: string | null;
  pageSize: number;
  loadedCount: number;
  hasMore: boolean;
}

const initialState: NotificationState = {
  items: [],
  unreadCount: 0,
  loading: false,
  countLoading: false,
  error: null,
  pageSize: 20,
  loadedCount: 0,
  hasMore: true,
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
      const skip = payload?.skip ?? 0;
      const take = payload?.take ?? 20;
      const { data } = await notificationApi.getAll(skip, take);
      return { items: data, skip, take };
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
      state.countLoading = false;
      state.error = null;
      state.loadedCount = 0;
      state.hasMore = true;
    },
  },
  extraReducers: (builder) => {
    builder.addCase(fetchNotifications.pending, (state) => {
      state.loading = true;
      state.error = null;
    });
    builder.addCase(fetchNotifications.fulfilled, (state, { payload }) => {
      state.loading = false;
      if (payload.skip === 0) {
        state.items = payload.items;
      } else {
        const existingIds = new Set(state.items.map((item) => item.id));
        state.items.push(...payload.items.filter((item) => !existingIds.has(item.id)));
      }
      state.pageSize = payload.take;
      state.loadedCount = payload.skip + payload.items.length;
      state.hasMore = payload.items.length === payload.take;
    });
    builder.addCase(fetchNotifications.rejected, (state, { payload }) => {
      state.loading = false;
      state.error = payload as string;
    });

    builder.addCase(fetchUnreadCount.pending, (state) => {
      state.countLoading = true;
    });
    builder.addCase(fetchUnreadCount.fulfilled, (state, { payload }) => {
      state.countLoading = false;
      state.unreadCount = payload;
    });
    builder.addCase(fetchUnreadCount.rejected, (state, { payload }) => {
      state.countLoading = false;
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
      state.loadedCount = state.items.length;
    });
    builder.addCase(removeNotification.rejected, (state, { payload }) => {
      state.error = payload as string;
    });
  },
});

export const { clearNotificationError, resetNotifications } = notificationSlice.actions;

export default notificationSlice.reducer;
