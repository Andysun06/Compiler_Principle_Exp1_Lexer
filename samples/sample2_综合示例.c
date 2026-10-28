/* 综合示例：编译原理实验一 词法分析
   涵盖关键字、标识符、十进制/十六进制整型常量、
   双精度常量（含科学计数法）、字符串常量与两类注释 */
int main() {
    int count = 100;
    int mask = 0x1F;            // 十六进制整型常量
    double pi = 3.14159;
    double big = 12.2E+2;       // 科学计数法：小数点与指数符号都不可省
    double whole = 25.;         // 小数点后可以没有数字
    char* msg = "hello, lexer";
    int sum = 0;

    for (int i = 0; i < count; i = i + 1) {
        if (i >= 10 && i != 20) {
            sum = sum + i * 2 / 3;
        } else {
            sum = sum - 1;
        }
    }

    while (sum > mask) {
        sum = sum / 2;
    }
    return sum;
}
