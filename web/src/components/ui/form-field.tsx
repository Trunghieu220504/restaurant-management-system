"use client";

import {
  useController,
  type Control,
  type FieldValues,
  type Path,
} from "react-hook-form";
import { cn } from "@/lib/utils";

type FormFieldProps<TValues extends FieldValues> = {
  control: Control<TValues>;
  name: Path<TValues>;
  label: string;
  type?: "text" | "email" | "password";
  placeholder?: string;
  autoComplete?: string;
};

function FormField<TValues extends FieldValues>({
  control,
  name,
  label,
  type = "text",
  placeholder,
  autoComplete,
}: FormFieldProps<TValues>) {
  const { field, fieldState } = useController({ control, name });
  const errorId = `${String(name).replaceAll(".", "-")}-error`;

  return (
    <div className="grid gap-2">
      <label htmlFor={field.name} className="text-sm font-medium">
        {label}
      </label>
      <input
        {...field}
        id={field.name}
        type={type}
        placeholder={placeholder}
        autoComplete={autoComplete}
        aria-invalid={fieldState.invalid}
        aria-describedby={fieldState.error ? errorId : undefined}
        className={cn(
          "h-10 w-full rounded-md border bg-background px-3 text-sm outline-none placeholder:text-muted-foreground focus-visible:ring-2 focus-visible:ring-neutral-400",
          fieldState.invalid && "border-red-600",
        )}
      />
      {fieldState.error && (
        <p id={errorId} role="alert" className="text-sm text-red-600">
          {fieldState.error.message}
        </p>
      )}
    </div>
  );
}

export { FormField };
