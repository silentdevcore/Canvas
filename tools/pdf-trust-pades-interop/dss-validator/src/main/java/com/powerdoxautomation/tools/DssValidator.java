package com.powerdoxautomation.tools;

import eu.europa.esig.dss.model.FileDocument;
import eu.europa.esig.dss.model.x509.CertificateToken;
import eu.europa.esig.dss.spi.DSSUtils;
import eu.europa.esig.dss.spi.validation.CommonCertificateVerifier;
import eu.europa.esig.dss.spi.x509.CommonTrustedCertificateSource;
import eu.europa.esig.dss.spi.x509.revocation.crl.ExternalResourcesCRLSource;
import eu.europa.esig.dss.validation.SignedDocumentValidator;
import eu.europa.esig.dss.validation.reports.Reports;

import java.io.File;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;

public final class DssValidator {
    private DssValidator() {
    }

    public static void main(String[] args) throws Exception {
        if (args.length != 4) {
            throw new IllegalArgumentException("Usage: DssValidator <pdf> <root-anchor> <root-crl> <output-directory>");
        }

        Path pdf = Path.of(args[0]).toAbsolutePath();
        Path rootAnchor = Path.of(args[1]).toAbsolutePath();
        Path rootCrl = Path.of(args[2]).toAbsolutePath();
        Path output = Path.of(args[3]).toAbsolutePath();
        Files.createDirectories(output);

        CommonTrustedCertificateSource trusted = new CommonTrustedCertificateSource();
        CertificateToken rootCertificate = DSSUtils.loadCertificate(rootAnchor.toFile());
        trusted.addCertificate(rootCertificate);

        CommonCertificateVerifier verifier = new CommonCertificateVerifier();
        verifier.setTrustedCertSources(trusted);
        verifier.setCrlSource(new ExternalResourcesCRLSource(new FileDocument(rootCrl.toFile())));
        SignedDocumentValidator validator = SignedDocumentValidator.fromDocument(new FileDocument(pdf.toFile()));
        validator.setCertificateVerifier(verifier);
        Reports reports = validator.validateDocument();

        String stem = stripPdfExtension(pdf.getFileName().toString());
        write(output.resolve(stem + "-dss-simple.xml"), reports.getXmlSimpleReport());
        write(output.resolve(stem + "-dss-detailed.xml"), reports.getXmlDetailedReport());
        write(output.resolve(stem + "-dss-diagnostic.xml"), reports.getXmlDiagnosticData());
        System.out.println("Validated " + pdf.getFileName() + " with EU DSS 6.4.");
    }

    private static void write(Path path, String content) throws Exception {
        String normalized = content.endsWith(System.lineSeparator())
                ? content
                : content + System.lineSeparator();
        Files.writeString(path, normalized, StandardCharsets.UTF_8);
    }

    private static String stripPdfExtension(String name) {
        return name.toLowerCase().endsWith(".pdf") ? name.substring(0, name.length() - 4) : name;
    }
}
