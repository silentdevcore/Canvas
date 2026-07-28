export default {
  server: {
    proxy: {
      '/api': {
        target: 'http://localhost:5086',
        changeOrigin: true,
      },
    },
    fs: {
      allow: ['../..'],
    },
  },
};
