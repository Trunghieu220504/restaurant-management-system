"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useForm } from "react-hook-form";
import { toast } from "sonner";
import { z } from "zod";
import { FormField } from "@/components/ui/form-field";
import { Button } from "@/components/ui/button";

const restaurantSchema = z.object({
  name: z.string().trim().min(2, "Tên cần có ít nhất 2 ký tự."),
  email: z.string().trim().email("Vui lòng nhập email hợp lệ."),
});

type RestaurantFormValues = z.infer<typeof restaurantSchema>;

export function RestaurantForm() {
  const form = useForm<RestaurantFormValues>({
    resolver: zodResolver(restaurantSchema),
    defaultValues: { name: "", email: "" },
  });

  function onSubmit(values: RestaurantFormValues) {
    toast.info(`Dữ liệu hợp lệ cho ${values.name}; biểu mẫu chưa kết nối API.`);
    form.reset();
  }

  return (
    <form className="grid gap-4" onSubmit={form.handleSubmit(onSubmit)} noValidate>
      <FormField
        autoComplete="organization"
        control={form.control}
        label="Tên nhà hàng"
        name="name"
        placeholder="Nhà hàng của bạn"
      />
      <FormField
        autoComplete="email"
        control={form.control}
        label="Email liên hệ"
        name="email"
        placeholder="ten@example.com"
        type="email"
      />
      <Button className="w-fit" disabled={form.formState.isSubmitting} type="submit">
        {form.formState.isSubmitting ? "Đang kiểm tra..." : "Kiểm tra biểu mẫu"}
      </Button>
    </form>
  );
}
