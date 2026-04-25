/** @type {import('tailwindcss').Config} */
export default {
  darkMode: ['class'],
  content: ['./index.html', './src/**/*.{js,ts,jsx,tsx}'],
  theme: {
    extend: {
      colors: {
        primary: {
          DEFAULT: '#06D6A0',
          50:  '#EDFDF8',
          100: '#D1FAF0',
          200: '#A3F4E1',
          300: '#6BEAD0',
          400: '#34D9BC',
          500: '#06D6A0',
          600: '#05BF90',
          700: '#049A74',
          800: '#037558',
          900: '#025840',
        },
        success: { DEFAULT: '#22C55E', 50: '#F0FDF4', 100: '#DCFCE7', 500: '#22C55E', 600: '#16A34A' },
        warning: { DEFAULT: '#F59E0B', 50: '#FFFBEB', 100: '#FEF3C7', 500: '#F59E0B', 600: '#D97706' },
        danger:  { DEFAULT: '#EF4444', 50: '#FEF2F2', 100: '#FEE2E2', 500: '#EF4444', 600: '#DC2626' },
        gold: {
          DEFAULT: '#F5C518',
          50:  '#FEFCE8',
          100: '#FEF9C3',
          500: '#F5C518',
          600: '#D4A017',
        },
        background: '#080B10',
        surface: '#0E1117',
        'surface-raised': '#131820',
        'surface-secondary': '#131820',
        bg: {
          primary:   '#080B10',
          secondary: '#0E1117',
          tertiary:  '#131820',
        },
        border: {
          DEFAULT: '#1A2233',
          primary: '#1E3040',
        },
        'text-primary': '#E2E8F0',
        'text-muted':   '#8B9AB0',
        'text-subtle':  '#3D4A5C',
      },
      fontFamily: {
        sans:    ['"DM Sans"', 'system-ui', 'sans-serif'],
        display: ['"Syne"', 'system-ui', 'sans-serif'],
        mono:    ['"JetBrains Mono"', 'monospace'],
      },
      keyframes: {
        shimmer: {
          '0%':   { backgroundPosition: '-200% 0' },
          '100%': { backgroundPosition:  '200% 0' },
        },
        'fade-in': {
          '0%':   { opacity: '0', transform: 'translateY(4px)' },
          '100%': { opacity: '1', transform: 'translateY(0)' },
        },
        'glow-pulse': {
          '0%, 100%': { opacity: '0.6' },
          '50%':      { opacity: '1' },
        },
      },
      animation: {
        shimmer:     'shimmer 2s infinite linear',
        'fade-in':   'fade-in 0.2s ease-out',
        'glow-pulse': 'glow-pulse 3s infinite ease-in-out',
      },
    },
  },
  plugins: [],
};
