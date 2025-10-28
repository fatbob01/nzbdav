import { createCookieSessionStorage } from "react-router";
import crypto from "crypto"
import { backendClient } from "~/clients/backend-client.server";

type User = {
  username: string;
};

export const sessionStorage = createCookieSessionStorage({
  cookie: {
    name: "__session",
    httpOnly: true,
    path: "/",
    sameSite: "strict",
    secrets: [process?.env?.SESSION_KEY || crypto.randomBytes(64).toString('hex')],
    secure: ["true", "yes"].includes(process?.env?.SECURE_COOKIES || ""),
  },
});

export async function authenticate(request: Request): Promise<User> {
  const formData = await request.formData();
  const username = formData.get("username")?.toString();
  const password = formData.get("password")?.toString();
  if (!username || !password) throw new Error("username and password required");
  if (await backendClient.authenticate(username, password)) return { username: username };
  throw new Error("Invalid credentials");
}