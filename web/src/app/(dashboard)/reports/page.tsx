"use client";
import Link from "next/link";
import { useState } from "react";
import type { components } from "@/lib/api/generated";
import { ReadState, useRead } from "@/features/management/api";
import s from "@/features/management/management.module.css";
type Report = components["schemas"]["BusinessReport"];
const money = (n: number | string) => Number(n).toLocaleString("vi-VN") + " đ";
export default function Reports() {
    const today = new Intl.DateTimeFormat("sv-SE", {
        timeZone: "Asia/Ho_Chi_Minh",
    }).format(new Date());
    const [from, setFrom] = useState(today),
        [to, setTo] = useState(today),
        [range, setRange] = useState([today, today]);
    const [start, end] = range;
    const result = useRead<Report>(
        start === end
            ? "/api/reporting/daily?date=" + start
            : "/api/reporting/range?from=" + start + "&to=" + end,
    );
    const r = result.data;
    return (
        <div className={s.page}>
            <header>
                <h1>Báo cáo KD</h1>
                <Link href="/more">Về mục Thêm</Link>
            </header>
            <form
                className={s.card}
                onSubmit={(e) => {
                    e.preventDefault();
                    setRange([from, to]);
                    if (from === start && to === end) result.reload();
                }}
            >
                <div className={s.toolbar}>
                    <label>
                        Từ ngày
                        <input
                            type="date"
                            required
                            value={from}
                            onChange={(e) => setFrom(e.target.value)}
                        />
                    </label>
                    <label>
                        Đến ngày
                        <input
                            type="date"
                            required
                            min={from}
                            value={to}
                            onChange={(e) => setTo(e.target.value)}
                        />
                    </label>
                    <button type="submit">Xem báo cáo</button>
                </div>
            </form>
            <ReadState {...result} />
            {r &&
                (r.days.length === 0 ? (
                    <p className={s.card}>
                        Chưa có ngày đã chốt trong kỳ đã chọn.
                    </p>
                ) : (
                    <>
                        <div className={s.cols}>
                            <section className={s.card}>
                                <h2>Doanh thu</h2>
                                <dl>
                                    <div>
                                        <dt>Doanh thu thuần</dt>
                                        <dd>{money(r.netRevenue)}</dd>
                                    </div>
                                    <div>
                                        <dt>Giá vốn</dt>
                                        <dd>{money(r.cost)}</dd>
                                    </div>
                                    <div>
                                        <dt>Lợi nhuận gộp</dt>
                                        <dd>{money(r.grossProfit)}</dd>
                                    </div>
                                </dl>
                            </section>
                            <section className={s.card}>
                                <h2>Dòng tiền</h2>
                                <dl>
                                    <div>
                                        <dt>Tiền đã thu</dt>
                                        <dd>
                                            {money(
                                                r.days.reduce(
                                                    (a, d) =>
                                                        a + Number(d.receipts),
                                                    0,
                                                ),
                                            )}
                                        </dd>
                                    </div>
                                    <div>
                                        <dt>Tiền đã hoàn</dt>
                                        <dd>
                                            {money(
                                                r.days.reduce(
                                                    (a, d) =>
                                                        a + Number(d.refunds),
                                                    0,
                                                ),
                                            )}
                                        </dd>
                                    </div>
                                    <div>
                                        <dt>Chi mua từ két</dt>
                                        <dd>{money(r.drawerPurchases)}</dd>
                                    </div>
                                </dl>
                            </section>
                        </div>
                        <section className={s.card}>
                            <h2>Chi tiết theo ngày</h2>
                            <div className={s.scroll}>
                                <table>
                                    <thead>
                                        <tr>
                                            {[
                                                "Ngày",
                                                "Doanh thu thuần",
                                                "Đã thu",
                                                "Đã hoàn",
                                                "Chi mua từ két",
                                            ].map((h) => (
                                                <th key={h}>{h}</th>
                                            ))}
                                        </tr>
                                    </thead>
                                    <tbody>
                                        {r.days.map((d) => (
                                            <tr key={d.dayId}>
                                                <th scope="row">{d.date}</th>
                                                {[
                                                    d.netRevenue,
                                                    d.receipts,
                                                    d.refunds,
                                                    d.drawerPurchases,
                                                ].map((v, i) => (
                                                    <td key={i}>{money(v)}</td>
                                                ))}
                                            </tr>
                                        ))}
                                    </tbody>
                                </table>
                            </div>
                        </section>
                    </>
                ))}
        </div>
    );
}
