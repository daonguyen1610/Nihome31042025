export const isValidVietnameseOverrideReason = (value?: string | null) => {
  const reason = value?.trim() ?? "";
  return reason.length >= 10
    && reason.length <= 500
    && /[ăâđêôơưĂÂĐÊÔƠƯáàảãạấầẩẫậắằẳẵặéèẻẽẹếềểễệíìỉĩịóòỏõọốồỗộớờởỡợúùủũụứừửữựýỳỷỹỵ]/.test(reason);
};
