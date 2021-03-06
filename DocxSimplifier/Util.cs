﻿/*****************************************************************************
* Produto: DocxSimplifier                                                    *
* Copyright (C) 2018  devCoffee Sistemas de Gestão Integrada                 *
*                                                                            *
* Este arquivo é parte do DocxSimplifier que é software livre; você pode     *
* redistribuí-lo e/ou modificá-lo sob os termos da Licença Pública Geral GNU,*
* conforme publicada pela Free Software Foundation; tanto a versão 3 da      *
* Licença como (a seu critério) qualquer versão mais nova.                   *
*                                                                            *
*                                                                            *
* Este programa é distribuído na expectativa de ser útil, mas SEM            *
* QUALQUER GARANTIA; sem mesmo a garantia implícita de                       *
* COMERCIALIZAÇÃO ou de ADEQUAÇÃO A QUALQUER PROPÓSITO EM                    *
* PARTICULAR. Consulte a Licença Pública Geral GNU para obter mais           *
* detalhes.                                                                  *
*                                                                            *
* Você deve ter recebido uma cópia da Licença Pública Geral GNU              *
* junto com este programa; se não, escreva para a Free Software              *
* Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA                   *
* 02111-1307, USA  ou para devCoffee Sistemas de Gestão Integrada,           *
* Rua Paulo Rebessi 665 - Cidade Jardim - Leme/SP.                           *
 ****************************************************************************/

using OpenXmlPowerTools;
using DocumentFormat.OpenXml.Packaging;
using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using NPOI.XWPF.UserModel;

namespace DocxSimplifier
{
    /*
    * @author Pedro Pozzi Ferreira @PozziSan
    * All the functionalities of the application are implemented in this class. 
    * Workflow:
    *   SimplifyMarkup(): Uses MarkupSimplifier to remove errorProof and RSID tags from the XML.
    *   Then, it gets all the paragraphs and store in xmlElement
    *   If the formatDocument Boolean is true, the reWriteDocument method is called and all the 
    *   paragraphs of the document are written into a new file, with the same name.
    * 
    */
    class Util
    {
        /// <summary>
        /// This method receives the main node of the document.xml file.
        /// From this node, the method runs recursively saving all the
        /// paragraphs on a new Xelement node
        /// </summary>
        /// <param name="node"></param>
        /// <param name="defaultParagraphStyleId"></param>
        /// <param name="z"></param>
        /// <returns></returns>
        private static object TransformToSimpleXml(XNode node, string defaultParagraphStyleId, string z)
        {
            XElement element = node as XElement;
            if (element != null)
            {
                if (element.Name == W.document)
                    return new XElement(z + "document",
                        new XAttribute(XNamespace.Xmlns + "w", z),
                        element.Element(W.body).Elements()
                            .Select(e => TransformToSimpleXml(e, defaultParagraphStyleId, z)));
                if (element.Name == W.p)
                {
                    string styleId = (string)element.Elements(W.pPr)
                        .Elements(W.pStyle).Attributes(W.val).FirstOrDefault();
                    if (styleId == null)
                        styleId = defaultParagraphStyleId;
                    return new XElement(z + "p",
                        new XAttribute("style", styleId),
                        element.LogicalChildrenContent(W.r).Elements(W.t).Select(t => (string)t)
                            .StringConcatenate());
                }
                if (element.Name == W.sdt)
                    return new XElement(z + "contentControl",
                        new XAttribute("tag", (string)element.Elements(W.sdtPr)
                            .Elements(W.tag).Attributes(W.val).FirstOrDefault()),
                        element.Elements(W.sdtContent).Elements()
                            .Select(e => TransformToSimpleXml(e, defaultParagraphStyleId, z)));
                return null;
            }
            return node;
        }

        /// <summary>
        /// This Functions Erases the Docx File and create a new one, using the
        /// simpleXml generated by the TransformToSimpleXml() method.
        /// </summary>
        /// <param name="docLocation"></param>
        /// <param name="simplerXml"></param>
        private static void ReWriteDocument(string docLocation, XElement simplerXml)
        {
            string newDocLocation = docLocation.Split('.')[0] + " Simplificado.docx";

            using (FileStream fileStream = new FileStream(newDocLocation, FileMode.Create, FileAccess.Write))
            {
                XWPFDocument newWordDoc = new XWPFDocument();

                foreach (XElement paragraph in simplerXml.Elements())
                {
                    XWPFParagraph newDocParagraph = newWordDoc.CreateParagraph();
                    newDocParagraph.Alignment = ParagraphAlignment.LEFT;
                    XWPFRun newDocRun = newDocParagraph.CreateRun();
                    newDocRun.FontFamily = "Arial";
                    newDocRun.FontSize = 12;
                    newDocRun.IsBold = false;
                    newDocRun.SetText(paragraph.Value);
                }

                newWordDoc.Write(fileStream);
                newWordDoc.Close();
            }

        }

        /// <summary>
        /// This method uses the MarkupSimplifier features from the OpenXMLPowerTools
        /// to remove the profile Errors and the RSID tags from Office, making the XML
        /// file cleaner to be processed to any other API
        /// </summary>
        /// <param name="docLocation"> The absolute location of the docx file</param>
        /// <param name="z">A namespace to be placed at the XML tags in the TransformToSimpleXml() method</param>
        /// <param name="formatDocument">Boolean indicating if the document should be or rewrited</param>
        public static void SimplifyMarkup(string docLocation, string z, bool formatDocument)
        {
            try
            {
                using (WordprocessingDocument wordDoc = WordprocessingDocument.Open(docLocation, true))
                {
                    RevisionAccepter.AcceptRevisions(wordDoc);

                    //Here I Define what components I want to clean from the XML. See all the attributes on the SimplifyMarkupSettings definitions
                    SimplifyMarkupSettings settings = new SimplifyMarkupSettings
                    {
                        RemoveComments = true,
                        RemoveContentControls = true,
                        RemoveEndAndFootNotes = true,
                        RemoveFieldCodes = false,
                        RemoveLastRenderedPageBreak = true,
                        RemovePermissions = true,
                        RemoveProof = true,
                        RemoveRsidInfo = true,
                        RemoveSmartTags = true,
                        RemoveSoftHyphens = true,
                        ReplaceTabsWithSpaces = true,
                        NormalizeXml = false,
                        RemoveWebHidden = true,
                        RemoveMarkupForDocumentComparison = true,

                    };

                    MarkupSimplifier.SimplifyMarkup(wordDoc, settings);

                    //Getting the deafult style of the document
                    string defaultParagraphStyleId = wordDoc.MainDocumentPart
                       .StyleDefinitionsPart.GetXDocument().Root.Elements(W.style)
                       .Where(e => (string)e.Attribute(W.type) == "paragraph" &&
                           (string)e.Attribute(W._default) == "1")
                       .Select(s => (string)s.Attribute(W.styleId))
                       .FirstOrDefault();
                    //Getting all the paragraphs in a xml node.
                    XElement simplerXml = (XElement)TransformToSimpleXml(
                        wordDoc.MainDocumentPart.GetXDocument().Root,
                        defaultParagraphStyleId, z);
                    Console.WriteLine(simplerXml);

                    wordDoc.Save();
                    wordDoc.Close();

                    //If formatDocument is true, the ReWriteDocument() method is called
                    if (formatDocument)
                    {
                        Console.WriteLine("Reescrevendo o documento sem estilos");
                        try
                        {
                            ReWriteDocument(docLocation, simplerXml);
                            Console.WriteLine("Sucesso ao Reformatar o documento!");

                        }
                        catch (Exception e)
                        {
                            throw new Exception(string.Format("Erro ao Reformatar o Arquivo: {0}", e.ToString()));
                        }
                    }

                }


            }
            catch (Exception e)
            {
                throw new Exception(string.Format("Não foi Possível simplificar o Arquivo. Erro: {0}", e.ToString()));
            }

        }
    }
}
