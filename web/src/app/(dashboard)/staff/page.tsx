"use client";

import { useState } from "react";
import { Plus, Search, Pencil, Menu, Users } from "lucide-react";
import { cn } from "@/lib/utils";

const staff = [
    {
        id: 1,
        name: "nguyen the vinh",
        phone: "+84902646559",
        role: "Chủ nhà hàng",
        avatar: null,
    },
    {
        id: 2,
        name: "Trần Văn An",
        phone: "+84901234567",
        role: "Nhân viên phục vụ",
        avatar: null,
    },
    {
        id: 3,
        name: "Lê Thị Bình",
        phone: "+84912345678",
        role: "Thu ngân",
        avatar: null,
    },
];

const departments = [
    { id: 1, name: "Bếp", memberCount: 3 },
    { id: 2, name: "Phục vụ", memberCount: 5 },
    { id: 3, name: "Thu ngân", memberCount: 2 },
];

type Tab = "staff" | "departments";

function getInitials(name: string): string {
    return name
        .split(" ")
        .map((n) => n[0])
        .slice(-2)
        .join("")
        .toUpperCase();
}

export default function StaffPage() {
    const [activeTab, setActiveTab] = useState<Tab>("staff");
    const [search, setSearch] = useState("");

    const filteredStaff = staff.filter(
        (s) =>
            s.name.toLowerCase().includes(search.toLowerCase()) ||
            s.phone.includes(search) ||
            s.role.toLowerCase().includes(search.toLowerCase()),
    );

    return (
        <div className="flex flex-col h-full bg-gray-50 min-h-screen">
            {/* Header */}
            <div className="flex items-center justify-between px-4 py-3 bg-white border-b border-gray-200">
                <h1 className="text-lg font-bold text-gray-800">
                    QL nhân viên
                </h1>
                <button className="p-1 rounded hover:bg-gray-100 transition-colors">
                    <Menu className="w-5 h-5 text-gray-600" />
                </button>
            </div>

            {/* Tabs */}
            <div className="flex mx-4 mt-3 rounded-lg overflow-hidden border border-gray-200 bg-gray-100">
                <button
                    onClick={() => setActiveTab("staff")}
                    className={cn(
                        "flex-1 py-2 text-sm font-semibold transition-colors rounded-l-lg",
                        activeTab === "staff"
                            ? "bg-gray-800 text-white"
                            : "bg-gray-100 text-gray-500 hover:bg-gray-200",
                    )}
                >
                    QL Nhân viên
                </button>
                <button
                    onClick={() => setActiveTab("departments")}
                    className={cn(
                        "flex-1 py-2 text-sm font-semibold transition-colors rounded-r-lg",
                        activeTab === "departments"
                            ? "bg-gray-800 text-white"
                            : "bg-gray-100 text-gray-500 hover:bg-gray-200",
                    )}
                >
                    QL Bộ phận
                </button>
            </div>

            {/* Content */}
            <div className="flex flex-col flex-1 px-4 mt-3 gap-3">
                {activeTab === "staff" && (
                    <>
                        {/* Add button */}
                        <button className="flex items-center justify-center gap-2 w-full py-2.5 rounded-lg bg-gray-200 text-gray-500 font-medium text-sm border border-gray-300 hover:bg-gray-300 transition-colors">
                            <Plus className="w-4 h-4" />
                            Thêm nhân viên mới
                        </button>

                        {/* Search */}
                        <div className="relative">
                            <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-400" />
                            <input
                                type="text"
                                placeholder="Tìm kiếm"
                                value={search}
                                onChange={(e) => setSearch(e.target.value)}
                                className="w-full pl-9 pr-4 py-2.5 rounded-lg border border-gray-200 bg-white text-sm text-gray-700 placeholder-gray-400 focus:outline-none focus:ring-2 focus:ring-gray-300"
                            />
                        </div>

                        {/* Staff list */}
                        <div className="bg-white rounded-xl border border-gray-200 overflow-hidden">
                            {filteredStaff.length === 0 ? (
                                <div className="py-10 text-center text-sm text-gray-400">
                                    Không tìm thấy nhân viên
                                </div>
                            ) : (
                                filteredStaff.map((member, index) => (
                                    <div
                                        key={member.id}
                                        className={cn(
                                            "flex items-center gap-3 px-4 py-3",
                                            index !==
                                                filteredStaff.length - 1 &&
                                                "border-b border-gray-100",
                                        )}
                                    >
                                        {/* Avatar */}
                                        <div className="w-10 h-10 rounded-full bg-gray-800 flex items-center justify-center flex-shrink-0">
                                            {member.avatar ? (
                                                // eslint-disable-next-line @next/next/no-img-element
                                                <img
                                                    src={member.avatar}
                                                    alt={member.name}
                                                    className="w-10 h-10 rounded-full object-cover"
                                                />
                                            ) : (
                                                <span className="text-white text-xs font-bold">
                                                    {getInitials(member.name)}
                                                </span>
                                            )}
                                        </div>

                                        {/* Info */}
                                        <div className="flex-1 min-w-0">
                                            <p className="text-sm font-bold text-gray-800 truncate">
                                                {member.name}
                                            </p>
                                            <p className="text-xs text-gray-500 truncate">
                                                {member.phone}
                                            </p>
                                            <p className="text-xs text-gray-400 truncate">
                                                {member.role}
                                            </p>
                                        </div>

                                        {/* Edit */}
                                        <button className="p-1.5 rounded hover:bg-gray-100 transition-colors flex-shrink-0">
                                            <Pencil className="w-4 h-4 text-gray-400" />
                                        </button>
                                    </div>
                                ))
                            )}
                        </div>
                    </>
                )}

                {activeTab === "departments" && (
                    <>
                        {/* Add department button */}
                        <button className="flex items-center justify-center gap-2 w-full py-2.5 rounded-lg bg-gray-200 text-gray-500 font-medium text-sm border border-gray-300 hover:bg-gray-300 transition-colors">
                            <Plus className="w-4 h-4" />
                            Thêm bộ phận mới
                        </button>

                        {/* Department list */}
                        <div className="bg-white rounded-xl border border-gray-200 overflow-hidden">
                            {departments.map((dept, index) => (
                                <div
                                    key={dept.id}
                                    className={cn(
                                        "flex items-center gap-3 px-4 py-3",
                                        index !== departments.length - 1 &&
                                            "border-b border-gray-100",
                                    )}
                                >
                                    {/* Icon */}
                                    <div className="w-10 h-10 rounded-full bg-gray-100 flex items-center justify-center flex-shrink-0">
                                        <Users className="w-5 h-5 text-gray-500" />
                                    </div>

                                    {/* Info */}
                                    <div className="flex-1 min-w-0">
                                        <p className="text-sm font-bold text-gray-800">
                                            {dept.name}
                                        </p>
                                        <p className="text-xs text-gray-500">
                                            {dept.memberCount} nhân viên
                                        </p>
                                    </div>

                                    {/* Edit */}
                                    <button className="p-1.5 rounded hover:bg-gray-100 transition-colors flex-shrink-0">
                                        <Pencil className="w-4 h-4 text-gray-400" />
                                    </button>
                                </div>
                            ))}
                        </div>
                    </>
                )}
            </div>
        </div>
    );
}
