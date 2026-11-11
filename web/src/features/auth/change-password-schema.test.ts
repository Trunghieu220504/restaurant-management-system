import { describe, expect, it } from "vitest";
import {
    changePasswordErrorMessage,
    changePasswordSchema,
    newPasswordSchema,
} from "./change-password-schema";

const valid = {
    currentPassword: "OldPassword1!",
    newPassword: "NewStrongPass456!",
    confirmPassword: "NewStrongPass456!",
};

describe("change password form rules", () => {
    it("accepts a password that meets the backend policy", () => {
        expect(changePasswordSchema.safeParse(valid).success).toBe(true);
    });

    it.each([
        ["too short", "Short1!aB"],
        ["no lowercase", "NOLOWERCASE123!"],
        ["no uppercase", "nouppercase123!"],
        ["no digit", "NoDigitsHere!!!"],
        ["no special character", "NoSpecial12345A"],
    ])("rejects a new password with %s", (_case, password) => {
        expect(newPasswordSchema.safeParse(password).success).toBe(false);
    });

    it("counts Vietnamese letters as special characters like ASP.NET Identity", () => {
        expect(newPasswordSchema.safeParse("Matkhaumoi123ă").success).toBe(
            true,
        );
    });

    it("requires the confirmation to match", () => {
        const result = changePasswordSchema.safeParse({
            ...valid,
            confirmPassword: "Different456!!",
        });
        expect(result.success).toBe(false);
        expect(result.error?.issues[0]?.path).toEqual(["confirmPassword"]);
    });

    it("rejects reusing the current password", () => {
        const result = changePasswordSchema.safeParse({
            currentPassword: valid.newPassword,
            newPassword: valid.newPassword,
            confirmPassword: valid.newPassword,
        });
        expect(result.success).toBe(false);
        expect(result.error?.issues[0]?.path).toEqual(["newPassword"]);
    });

    it("never says which field was wrong on a 400", () => {
        const message = changePasswordErrorMessage(400);
        expect(message).toContain("mật khẩu hiện tại");
        expect(message).toContain("mật khẩu mới");
        expect(changePasswordErrorMessage(429)).toContain("một phút");
        expect(changePasswordErrorMessage(undefined)).toContain("máy chủ");
    });
});
