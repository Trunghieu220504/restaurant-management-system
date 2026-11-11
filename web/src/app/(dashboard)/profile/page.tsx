"use client";

import { useState } from "react";
import { ArrowLeft, Pencil, ChevronDown } from "lucide-react";
import { useRouter } from "next/navigation";
import { useAuth } from "@/components/auth/auth-provider";

export default function ProfilePage() {
  const router = useRouter();
  const { user, logout } = useAuth();

  const [keepScreenOn, setKeepScreenOn] = useState(false);
  const [language, setLanguage] = useState("Tiếng Việt");
  const [langOpen, setLangOpen] = useState(false);

  const languages = ["Tiếng Việt", "English"];

  const profileFields = [
    { label: "Số điện thoại", value: "+84902646559" },
    { label: "Họ tên", value: user?.userName || "nguyen the vinh" },
    { label: "Email", value: user?.email || "Nhập Email", placeholder: !user?.email },
    { label: "Mật khẩu", value: "Thay đổi mật khẩu", placeholder: true },
  ];

  return (
    <div className="min-h-screen bg-gray-50">
      {/* Header */}
      <div className="bg-white shadow-sm flex items-center gap-3 px-4 py-4 sticky top-0 z-10">
        <button
          onClick={() => router.back()}
          className="p-1 rounded-full hover:bg-gray-100 transition-colors"
          aria-label="Quay lại"
        >
          <ArrowLeft className="w-5 h-5 text-gray-700" />
        </button>
        <h1 className="text-lg font-semibold text-gray-800">Hồ sơ</h1>
      </div>

      <div className="max-w-lg mx-auto px-4 py-6 space-y-4">
        {/* Profile rows */}
        <div className="bg-white rounded-lg shadow-sm divide-y divide-gray-100">
          {profileFields.map((field) => (
            <div
              key={field.label}
              className="flex items-center justify-between px-4 py-4"
            >
              <span className="text-sm text-gray-500 w-32 shrink-0">
                {field.label}
              </span>
              <div className="flex items-center gap-2 flex-1 justify-end">
                <span
                  className={`text-sm font-medium ${
                    field.placeholder ? "text-gray-400" : "text-gray-800"
                  }`}
                >
                  {field.value}
                </span>
                <Pencil className="w-4 h-4 text-red-500 shrink-0 cursor-pointer" />
              </div>
            </div>
          ))}
        </div>

        {/* Setting toggles */}
        <div className="bg-white rounded-lg shadow-sm divide-y divide-gray-100">
          {/* Keep screen on toggle */}
          <div className="flex items-center justify-between px-4 py-4">
            <span className="text-sm text-gray-700">
              Giữ màn hình luôn sáng khi dùng ứng dụng
            </span>
            <button
              onClick={() => setKeepScreenOn(!keepScreenOn)}
              className={`w-12 h-6 flex items-center rounded-full p-1 transition-colors duration-200 ease-in-out ${
                keepScreenOn ? "bg-red-500" : "bg-gray-200"
              }`}
            >
              <div
                className={`bg-white w-4 h-4 rounded-full shadow-md transform transition-transform duration-200 ease-in-out ${
                  keepScreenOn ? "translate-x-6" : "translate-x-0"
                }`}
              />
            </button>
          </div>

          {/* Language select */}
          <div className="px-4 py-4">
            <div className="flex items-center justify-between">
              <span className="text-sm text-gray-700">Ngôn ngữ</span>
              <button
                onClick={() => setLangOpen(!langOpen)}
                className="flex items-center gap-2 text-sm text-gray-800 font-medium px-3 py-1.5 rounded-md bg-gray-100 hover:bg-gray-200 transition-colors"
              >
                {language}
                <ChevronDown className="w-4 h-4 text-gray-500" />
              </button>
            </div>

            {langOpen && (
              <div className="mt-2 bg-white border border-gray-100 rounded-md shadow-md overflow-hidden">
                {languages.map((lang) => (
                  <button
                    key={lang}
                    onClick={() => {
                      setLanguage(lang);
                      setLangOpen(false);
                    }}
                    className={`w-full text-left px-4 py-2 text-sm transition-colors hover:bg-gray-50 ${
                      lang === language
                        ? "text-red-500 font-medium"
                        : "text-gray-700"
                    }`}
                  >
                    {lang}
                  </button>
                ))}
              </div>
            )}
          </div>
        </div>

        {/* App version */}
        <div className="bg-white rounded-lg shadow-sm px-4 py-4 flex items-center justify-between">
          <span className="text-sm text-gray-700">Phiên bản</span>
          <span className="text-sm text-gray-500">1.25.90</span>
        </div>

        {/* Action buttons */}
        <div className="flex gap-3 pt-2">
          <button
            onClick={() => logout()}
            className="flex-1 py-3 rounded-lg border-2 border-red-500 text-red-500 text-sm font-semibold hover:bg-red-50 transition-colors"
          >
            Đăng xuất
          </button>
          <button
            onClick={() => logout()}
            className="flex-1 py-3 rounded-lg border-2 border-red-500 text-red-500 text-sm font-semibold hover:bg-red-50 transition-colors"
          >
            Xóa tài khoản
          </button>
        </div>
      </div>
    </div>
  );
}
