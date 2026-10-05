// 中文翻译，每个区域一个文件（放在 ./zh/ 下）。键是代码中使用的法语源字符串。
const modules = import.meta.glob('./zh/*.js', { eager: true });

export default Object.assign({}, ...Object.values(modules).map((m) => m.default));
