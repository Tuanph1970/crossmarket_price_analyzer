/** @type {import('tailwindcss').Config} */
export default {
  darkMode: ['class'],
  content: ['./index.html', './src/**/*.{js,ts,jsx,tsx}'],
  theme: {
    extend: {
      colors: {
        primary: {
          DEFAULT: '#2563EB',
          50: '#EFF6FF',
          100: '#DBEAFE',
          200: '#BFDBFE',
          300: '#93C5FD',
          400: '#60A5FA',
          500: '#2563EB',
          600: '#1D4ED8',
          700: '#1E40AF',
          800: '#1E3A8A',
          900: '#1E3A8A',
        },
        success: { DEFAULT: '#16A34A', 50: '#F0FDF4', 100: '#DCFCE7', 500: '#16A34A', 600: '#15803D' },
        warning: { DEFAULT: '#D97706', 50: '#FFFBEB', 100: '#FEF3C7', 500: '#D97706', 600: '#B45309' },
        danger:  { DEFAULT: '#DC2626', 50: '#FEF2F2', 100: '#FEE2E2', 500: '#DC2626', 600: '#B91C1C' },
        background: '#F9FAFB',
        surface: '#FFFFFF',
        'text-primary': '#111827',
        'text-muted': '#6B7280',
      },
      fontFamily: {
        sans: ['Inter', 'system-ui', 'sans-serif'],
      },
    },
  },
  plugins: [],
};
