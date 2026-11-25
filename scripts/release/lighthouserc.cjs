module.exports = {
    ci: {
        collect: {
            url: [process.env.LIGHTHOUSE_QR_URL],
            numberOfRuns: 3,
            settings: { preset: 'perf', formFactor: 'mobile', screenEmulation: { mobile: true, width: 360, height: 800, deviceScaleFactor: 1, disabled: false } },
        },
        assert: {
            assertions: {
                'categories:performance': ['error', { minScore: 0.8, aggregationMethod: 'median' }],
                'categories:accessibility': ['error', { minScore: 0.9, aggregationMethod: 'median' }],
            },
        },
        upload: { target: 'filesystem', outputDir: 'artifacts/release/lighthouse' },
    },
};
if (!/^https:\/\//.test(process.env.LIGHTHOUSE_QR_URL || '')) throw new Error('Set a real HTTPS QR page in LIGHTHOUSE_QR_URL');
