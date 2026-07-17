import {defineConfig} from '@playwright/test';
export default defineConfig({testDir:'.',testMatch:'*.spec.ts',use:{baseURL:'http://127.0.0.1:5173',trace:'retain-on-failure'},reporter:'list'});
