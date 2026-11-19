/** All amounts are whole VND; avoid silently rounding an int64 in the browser. */
export function integerValue(
    value: FormDataEntryValue | null,
    label: string,
    min = 0,
    max = Number.MAX_SAFE_INTEGER,
): number {
    const text = typeof value === "string" ? value.trim() : "";
    const number = Number(text);
    if (
        !/^\d+$/.test(text) ||
        !Number.isSafeInteger(number) ||
        number < min ||
        number > max
    ) {
        throw new Error(
            `${label} phải là số nguyên từ ${min.toLocaleString("vi-VN")} đến ${max.toLocaleString("vi-VN")}.`,
        );
    }
    return number;
}

export function requiredText(
    value: FormDataEntryValue | null,
    label: string,
    max = 200,
): string {
    const text = typeof value === "string" ? value.trim() : "";
    if (!text || text.length > max)
        throw new Error(`${label} cần có từ 1 đến ${max} ký tự.`);
    return text;
}

export function selectionLimits(min: number, max: number, available: number) {
    if (min > max)
        throw new Error("Số lựa chọn tối thiểu không được lớn hơn tối đa.");
    if (max < 1) throw new Error("Số lựa chọn tối đa phải từ 1 trở lên.");
    if (max > available)
        throw new Error(
            `Nhóm chỉ có ${available} lựa chọn đang sử dụng. Hãy giảm tối đa hoặc thêm lựa chọn.`,
        );
    return { minSelections: min, maxSelections: max };
}
