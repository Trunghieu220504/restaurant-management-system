export function displayDate(iso: string) {
    return iso.replace(/^(\d{4})-(\d{2})-(\d{2})$/, "$3/$2/$1");
}

export function isoDate(text: string) {
    const match = /^(\d{2})\/(\d{2})\/(\d{4})$/.exec(text);
    if (!match) return "";
    const [, day, month, year] = match;
    const iso = year + "-" + month + "-" + day;
    const date = new Date(iso + "T00:00:00Z");
    return +year > 0 &&
        Number.isFinite(date.getTime()) &&
        date.toISOString().slice(0, 10) === iso
        ? iso
        : "";
}

export const vietnamDateTime = new Intl.DateTimeFormat("vi-VN", {
    timeZone: "Asia/Ho_Chi_Minh",
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
    hour: "2-digit",
    minute: "2-digit",
    hourCycle: "h23",
});
