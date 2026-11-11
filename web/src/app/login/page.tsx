"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import Link from "next/link";
import { useAuth } from "@/components/auth/auth-provider";
import { User, Lock, Eye, EyeOff, AlertCircle, QrCode } from "lucide-react";

export default function LoginPage() {
  const router = useRouter();
  const { login } = useAuth();
  const [phone, setPhone] = useState("");
  const [password, setPassword] = useState("");
  const [showPassword, setShowPassword] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (loading) return;
    setError(null);
    setLoading(true);

    try {
      // Map phone to backend user or use standard username
      const username = phone;
      await login(username, password);
      // Chuyển sang màn hình Chọn / Quản lý Cửa hàng theo đúng sơ đồ luồng
      router.push("/sales-day");
    } catch {
      setError("Đăng nhập thất bại. Kiểm tra tài khoản, mật khẩu hoặc thử lại sau.");
    } finally {
      setLoading(false);
    }
  };


  return (
    <div className="relative min-h-screen bg-white flex flex-col items-center justify-center p-4 overflow-hidden">
      {/* Soft Pink Background Decorative Circles from Design */}
      <div className="absolute -top-16 -left-16 w-72 h-72 rounded-full bg-pink-100/70 blur-2xl pointer-events-none" />
      <div className="absolute -top-12 -right-12 w-64 h-64 rounded-full bg-rose-100/60 blur-2xl pointer-events-none" />
      <div className="absolute -bottom-20 -right-20 w-80 h-80 rounded-full bg-pink-100/70 blur-3xl pointer-events-none" />
      <div className="absolute -bottom-16 -left-16 w-60 h-60 rounded-full bg-rose-50/80 blur-2xl pointer-events-none" />

      {/* Main Container */}
      <div className="relative z-10 max-w-sm w-full mx-auto space-y-6">
        {/* Title matching design */}
        <div className="text-center pt-4">
          <h1 className="text-2xl font-medium text-rose-500 tracking-tight">
            Đăng nhập
          </h1>
        </div>

        {/* Error notification */}
        {error && (
          <div role="alert" className="bg-red-50 border border-red-200 text-red-700 text-xs p-3 rounded-2xl flex items-center gap-2">
            <AlertCircle size={16} className="shrink-0 text-red-500" />
            <span>{error}</span>
          </div>
        )}

        {/* Form matching uploaded mockup image */}
        <form onSubmit={handleSubmit} className="space-y-4">
          {/* Phone input */}
          <div className="relative">
            <div className="absolute left-4 top-1/2 -translate-y-1/2 text-gray-400">
              <User size={20} className="stroke-[1.75]" />
            </div>
            <input
              type="text"
              required
              value={phone}
              aria-label="Tài khoản"
              autoComplete="username"
              maxLength={256}
              disabled={loading}
              onChange={(e) => setPhone(e.target.value)}
              placeholder="Tài khoản"
              className="w-full pl-12 pr-4 py-3.5 bg-gray-100/90 text-gray-800 placeholder-gray-400 rounded-2xl text-sm outline-none focus:ring-2 focus:ring-rose-400 focus:bg-white transition-all shadow-inner/10"
            />
          </div>

          {/* Password input */}
          <div className="relative">
            <div className="absolute left-4 top-1/2 -translate-y-1/2 text-gray-400">
              <Lock size={20} className="stroke-[1.75]" />
            </div>
            <input
              type={showPassword ? "text" : "password"}
              required
              value={password}
              aria-label="Mật khẩu"
              autoComplete="current-password"
              maxLength={1024}
              disabled={loading}
              onChange={(e) => setPassword(e.target.value)}
              placeholder="Mật khẩu"
              className="w-full pl-12 pr-12 py-3.5 bg-gray-100/90 text-gray-800 placeholder-gray-400 rounded-2xl text-sm outline-none focus:ring-2 focus:ring-rose-400 focus:bg-white transition-all shadow-inner/10"
            />
            <button
              type="button"
              onClick={() => setShowPassword(!showPassword)}
              className="absolute right-4 top-1/2 -translate-y-1/2 text-gray-400 hover:text-gray-600 transition-colors p-1"
            >
              {showPassword ? <EyeOff size={18} /> : <Eye size={18} />}
            </button>
          </div>

          {/* Forgot password link */}
          <div className="flex justify-end pt-0.5">
            <button
              type="button"
              onClick={() => alert("Chức năng khôi phục mật khẩu qua SMS OTP đang được kết nối.")}
              className="text-xs text-rose-500/90 hover:text-rose-600 font-normal transition-colors"
            >
              Bạn quên mật khẩu?
            </button>
          </div>

          {/* Red gradient submit button */}
          <div className="pt-2">
            <button
              type="submit"
              disabled={loading}
              className="w-full py-3.5 bg-gradient-to-r from-red-500 to-rose-600 hover:from-red-600 hover:to-rose-700 text-white font-medium text-base rounded-2xl shadow-lg shadow-red-500/30 active:scale-[0.99] transition-all disabled:opacity-60"
            >
              {loading ? "Đang xử lý..." : "Đăng nhập"}
            </button>
          </div>
        </form>


        {/* QR Customer Direct Access banner */}
        <div className="pt-2">
          <Link
            href="/guest?table=T04"
            className="flex items-center justify-center gap-2 p-3 bg-slate-50 hover:bg-slate-100 rounded-2xl border border-slate-200/80 text-xs text-slate-700 font-medium transition-colors group"
          >
            <QrCode size={16} className="text-rose-500 group-hover:scale-110 transition-transform" />
            <span>Trải nghiệm Khách quét mã QR tại bàn (Không cần đăng nhập)</span>
          </Link>
        </div>
      </div>
    </div>
  );
}
