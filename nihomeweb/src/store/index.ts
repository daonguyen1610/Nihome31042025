import { configureStore } from "@reduxjs/toolkit";
import { useDispatch, useSelector, type TypedUseSelectorHook } from "react-redux";
import authReducer from "./authSlice";
import notificationReducer from "./notificationSlice";
import { setStoreRef } from "@/lib/api";

export const store = configureStore({
  reducer: {
    auth: authReducer,
    notifications: notificationReducer,
  },
});

// Wire up the Axios interceptor so it can read the token
setStoreRef(store.getState.bind(store));

export type RootState = ReturnType<typeof store.getState>;
export type AppDispatch = typeof store.dispatch;

export const useAppDispatch: () => AppDispatch = useDispatch;
export const useAppSelector: TypedUseSelectorHook<RootState> = useSelector;
