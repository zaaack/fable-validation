module.exports = {
  testEnvironment: 'node',
  moduleFileExtensions: ['js', 'fs'],
  transform: {
    '^.+\\.(fs)$': 'jest-fable-preprocessor',
    '^.+\\.js$': 'jest-fable-preprocessor/source/babel-jest.js'
  },
  testMatch: ['**/**/*.(test.fs)'],
  coveragePathIgnorePatterns: ['/packages/', 'test/']
};
