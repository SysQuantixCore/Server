#include <algorithm>
#include <cctype>
#include <cstdio>
#include <fstream>
#include <iostream>
#include <iterator>
#include <string>
#include <vector>

namespace {

const unsigned char kXorKey[16] = {
    0x7C, 0xBD, 0x81, 0x9F, 0x3D, 0x93, 0xE2, 0x56,
    0x2A, 0x73, 0xD2, 0x3E, 0xF2, 0x83, 0x95, 0xBF
};

int XorConvertBuffer(unsigned char* buffer, int size, int keyIndex) {
    int sizeLeft = size;
    unsigned char* current = buffer;

    int convertSize = std::min((16 - keyIndex) % 16, size);

    for (int i = 0; i < convertSize; ++i) {
        current[i] ^= kXorKey[i + keyIndex];
    }

    current += convertSize;
    sizeLeft -= convertSize;

    if (sizeLeft <= 0) {
        return convertSize + keyIndex;
    }

    while (sizeLeft >= 16) {
        convertSize = 16;

        for (int i = 0; i < convertSize; ++i) {
            current[i] ^= kXorKey[i];
        }

        current += convertSize;
        sizeLeft -= convertSize;
    }

    convertSize = sizeLeft;

    for (int i = 0; i < convertSize; ++i) {
        current[i] ^= kXorKey[i];
    }

    return convertSize;
}

std::string ToLower(std::string value) {
    std::transform(value.begin(), value.end(), value.begin(), [](unsigned char c) {
        return static_cast<char>(std::tolower(c));
    });
    return value;
}

bool EndsWithIgnoreCase(const std::string& text, const std::string& suffix) {
    if (suffix.size() > text.size()) {
        return false;
    }

    std::string lowerText = ToLower(text);
    std::string lowerSuffix = ToLower(suffix);
    return lowerText.compare(lowerText.size() - lowerSuffix.size(), lowerSuffix.size(), lowerSuffix) == 0;
}

bool ShouldDecodeXor(const std::string& inputPath, bool forceDecode) {
    if (forceDecode) {
        return true;
    }

    return EndsWithIgnoreCase(inputPath, "muerror.log");
}

std::string BuildTextFromBuffer(const std::vector<unsigned char>& data) {
    return std::string(data.begin(), data.end());
}

void PrintUsage() {
    std::cout
        << "LogReaderVS2022 - leitor de logs MU\n"
        << "Uso:\n"
        << "  LogReaderVS2022.exe <arquivo_log> [saida_txt] [--xor]\n\n"
        << "Exemplos:\n"
        << "  LogReaderVS2022.exe \"..\\..\\Client_2\\MuError.log\"\n"
        << "  LogReaderVS2022.exe \"..\\..\\Client_2\\MuError.log\" \"MuError_decodificado.txt\"\n"
        << "  LogReaderVS2022.exe \"..\\..\\Client_2\\MuRuntime.log\"\n"
        << "  LogReaderVS2022.exe \"log.bin\" \"out.txt\" --xor\n";
}

} // namespace

int main(int argc, char* argv[]) {
    if (argc < 2) {
        PrintUsage();
        return 1;
    }

    const std::string inputPath = argv[1];
    std::string outputPath;
    bool forceDecode = false;

    for (int i = 2; i < argc; ++i) {
        const std::string arg = argv[i];
        if (arg == "--xor") {
            forceDecode = true;
        } else if (outputPath.empty()) {
            outputPath = arg;
        }
    }

    std::ifstream inFile(inputPath, std::ios::binary);
    if (!inFile) {
        std::cerr << "Erro ao abrir arquivo: " << inputPath << "\n";
        return 2;
    }

    std::vector<unsigned char> fileData((std::istreambuf_iterator<char>(inFile)),
        std::istreambuf_iterator<char>());

    if (fileData.empty()) {
        std::cout << "Arquivo vazio: " << inputPath << "\n";
        return 0;
    }

    const bool decodeXor = ShouldDecodeXor(inputPath, forceDecode);

    if (decodeXor) {
        int rollingKey = 0;
        // Decode all bytes using the same rolling key algorithm used by Main5.2 logger.
        rollingKey = XorConvertBuffer(fileData.data(), static_cast<int>(fileData.size()), rollingKey);
        (void)rollingKey;
    }

    const std::string text = BuildTextFromBuffer(fileData);

    if (outputPath.empty()) {
        std::cout << text;
        if (!text.empty() && text.back() != '\n') {
            std::cout << "\n";
        }
    } else {
        std::ofstream outFile(outputPath, std::ios::binary | std::ios::trunc);
        if (!outFile) {
            std::cerr << "Erro ao criar arquivo de saida: " << outputPath << "\n";
            return 3;
        }

        outFile.write(text.data(), static_cast<std::streamsize>(text.size()));
        outFile.close();

        std::cout << "OK - log processado com sucesso.\n"
                  << "Entrada: " << inputPath << "\n"
                  << "Saida:   " << outputPath << "\n"
                  << "Modo:    " << (decodeXor ? "XOR decode" : "texto puro") << "\n";
    }

    return 0;
}
